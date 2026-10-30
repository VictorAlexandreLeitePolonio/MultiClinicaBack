using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.MercadoPago;

// Aplica o estado de um pagamento do Mercado Pago na cobrança local.
// É a única porta de entrada de confirmação: chamado pela resposta do /pay e pelo webhook.
public sealed class MercadoPagoPaymentSync(
    AppDbContext db,
    IMercadoPagoClient client,
    MercadoPagoAccounts accounts,
    ILogger<MercadoPagoPaymentSync> logger)
{
    private const string ExternalReferencePrefix = "payment:";

    public async Task<Payment?> ApplyAsync(int clinicaId, long mpPaymentId, CancellationToken ct = default)
    {
        var connected = await accounts.GetConnectedAsync(clinicaId, ct);
        if (connected is null)
        {
            logger.LogWarning("Pagamento {MpPaymentId} ignorado: clínica {ClinicaId} sem Mercado Pago conectado.",
                mpPaymentId, clinicaId);
            return null;
        }

        MpPayment mp;
        try
        {
            mp = await client.GetPaymentAsync(connected.AccessToken, mpPaymentId, ct);
        }
        catch (MercadoPagoApiException exception) when (exception.StatusCode == 401)
        {
            await accounts.MarkRevokedAsync(clinicaId, ct);
            logger.LogWarning("Mercado Pago recusou o token da clínica {ClinicaId} ao consultar o pagamento {MpPaymentId}.",
                clinicaId, mpPaymentId);
            return null;
        }

        if (!TryParsePaymentId(mp.ExternalReference, out var paymentId))
        {
            logger.LogWarning("Pagamento {MpPaymentId} com external_reference inesperada: {ExternalReference}.",
                mpPaymentId, mp.ExternalReference);
            return null;
        }

        if (mp.CollectorId != connected.Account.MpUserId)
        {
            logger.LogWarning("Pagamento {MpPaymentId} pertence ao collector {CollectorId}, não à clínica {ClinicaId}.",
                mpPaymentId, mp.CollectorId, clinicaId);
            return null;
        }

        var payment = await db.Payments
            .Include(p => p.Clinica)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.ClinicaId == clinicaId && !p.IsDeleted, ct);
        if (payment is null)
        {
            logger.LogWarning("Pagamento {MpPaymentId} aponta para a cobrança {PaymentId}, que não existe na clínica {ClinicaId}.",
                mpPaymentId, paymentId, clinicaId);
            return null;
        }

        switch (mp.Status)
        {
            case "approved":
                if (payment.Status == PaymentStatus.Paid
                    && payment.MpPaymentId is { } alreadyPaidId
                    && alreadyPaidId != mp.Id)
                {
                    // Duas tentativas aprovadas para a mesma cobrança: a segunda precisa ser reembolsada.
                    payment.OnlineAlert = $"PagamentoDuplicado:{mp.Id}";
                    logger.LogWarning(
                        "Pagamento duplicado {MpPaymentId} para a cobrança {PaymentId} (já paga por {OriginalMpPaymentId}).",
                        mp.Id, payment.Id, alreadyPaidId);
                    break;
                }

                payment.Status = PaymentStatus.Paid;
                payment.PaidOnline = true;
                payment.PaidAt = ToClinicDate(mp.DateApproved ?? DateTime.UtcNow, payment.Clinica.TimeZoneId);
                if (MapPaymentMethod(mp.PaymentTypeId) is { } method)
                    payment.PaymentMethod = method;
                payment.MpPaymentId = mp.Id;
                payment.MpStatus = mp.Status;
                payment.MpStatusDetail = mp.StatusDetail;
                payment.OnlineAlert = null;
                break;

            case "pending":
            case "in_process":
            case "authorized":
            case "rejected":
            case "cancelled":
                // A cobrança continua pendente: o paciente pode tentar de novo.
                // Um pagamento que não é o principal (tentativa duplicada) não muda o estado da cobrança.
                if (payment.MpPaymentId is { } currentMpId && currentMpId != mp.Id)
                {
                    logger.LogInformation(
                        "Pagamento {MpPaymentId} ({Status}) é uma tentativa secundária da cobrança {PaymentId}; ignorado.",
                        mp.Id, mp.Status, payment.Id);
                    return payment;
                }
                payment.MpPaymentId = mp.Id;
                payment.MpStatus = mp.Status;
                payment.MpStatusDetail = mp.StatusDetail;
                break;

            case "refunded":
            case "charged_back":
                if (payment.MpPaymentId is { } paidMpId && paidMpId != mp.Id)
                {
                    logger.LogInformation(
                        "Estorno/contestamento do pagamento secundário {MpPaymentId} da cobrança {PaymentId}; ignorado.",
                        mp.Id, payment.Id);
                    return payment;
                }

                payment.MpStatus = mp.Status;
                payment.MpStatusDetail = mp.StatusDetail;
                if (payment.RefundedAt is not null)
                {
                    // Reembolso feito pelo painel: a cobrança já está cancelada e continua assim.
                    break;
                }

                payment.Status = PaymentStatus.Pending;
                payment.PaidOnline = false;
                payment.PaidAt = null;
                payment.OnlineAlert = mp.Status == "charged_back" ? "Contestado" : "Estornado";
                break;

            default:
                logger.LogInformation("Status do Mercado Pago não mapeado: {Status} ({MpPaymentId}).", mp.Status, mp.Id);
                payment.MpStatus = mp.Status;
                payment.MpStatusDetail = mp.StatusDetail;
                break;
        }

        await db.SaveChangesAsync(ct);
        return payment;
    }

    // "payment:123" → 123.
    private static bool TryParsePaymentId(string? externalReference, out int paymentId)
    {
        paymentId = 0;
        return externalReference is not null
            && externalReference.StartsWith(ExternalReferencePrefix, StringComparison.Ordinal)
            && int.TryParse(externalReference[ExternalReferencePrefix.Length..], out paymentId);
    }

    // O Mercado Pago devolve UTC; PaidAt é o dia civil da clínica.
    private static DateOnly ToClinicDate(DateTime value, string? timeZoneId)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }

    private static string? MapPaymentMethod(string? paymentTypeId) => paymentTypeId switch
    {
        "bank_transfer" => "Pix",
        "credit_card" => "Cartão de crédito",
        "debit_card" => "Cartão de débito",
        _ => null,
    };
}
