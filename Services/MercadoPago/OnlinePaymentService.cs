using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Payment;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;
using Npgsql;

namespace MultiClinica.API.Services.MercadoPago;

// Cobrança com link, pagamento público e reembolso. Os endpoints da clínica usam o
// usuário logado; o portal do paciente usa a conta autenticada.
public sealed class OnlinePaymentService(
    AppDbContext db,
    MercadoPagoAccounts accounts,
    IMercadoPagoClient client,
    MercadoPagoPaymentSync sync,
    IOptions<MercadoPagoOptions> options,
    IConfiguration configuration,
    IUsuarioLogadoService usuario,
    IPatientAccountLoggedService loggedPatient,
    TimeProvider time,
    ILogger<OnlinePaymentService> logger)
{
    private const string WebhookPath = "/api/payments/webhooks/mercadopago";
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private string FrontendUrl => (configuration["FRONTEND_URL"] ?? "https://cliniqcare.com.br").TrimEnd('/');

    // ── Clínica ──────────────────────────────────────────────────────────────

    public async Task<Result<bool>> SendAsync(int paymentId, CancellationToken ct = default)
    {
        var payment = await LoadClinicPaymentAsync(paymentId, ct);
        if (payment is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Cobrança não encontrada.");

        var check = await CheckSendableAsync(payment, ct);
        if (check.ErrorCode is not null)
            return Result<bool>.Fail(check.ErrorCode, check.Message!);

        if (string.IsNullOrWhiteSpace(payment.Patient.Email))
            return Result<bool>.Fail(ErrorCodes.PatientEmailRequired,
                "O paciente precisa ter um e-mail cadastrado para receber a cobrança.");

        await EnsureTokenAsync(payment, ct);
        var now = time.GetUtcNow().UtcDateTime;
        var key = $"payment-charge:{payment.Id}:{now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture)}";

        if (!await db.PatientEmailOutbox.AnyAsync(job => job.DeduplicationKey == key, ct))
        {
            db.PatientEmailOutbox.Add(new PatientEmailOutbox
            {
                ClinicaId = payment.ClinicaId,
                PatientId = payment.PatientId,
                PatientAccountId = payment.Patient.PatientAccountId,
                PaymentId = payment.Id,
                Type = PatientEmailOutboxType.PaymentCharge,
                DeduplicationKey = key,
                NextAttemptAt = now,
                CreatedByUserId = usuario.UserId,
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                // Duplo clique no mesmo minuto: a primeira requisição já enfileirou o e-mail.
            }
        }

        return Result<bool>.Ok(true);
    }

    public async Task<Result<PaymentLinkDto>> GetLinkAsync(int paymentId, CancellationToken ct = default)
    {
        var payment = await LoadClinicPaymentAsync(paymentId, ct);
        if (payment is null)
            return Result<PaymentLinkDto>.Fail(ErrorCodes.NotFound, "Cobrança não encontrada.");

        var check = await CheckSendableAsync(payment, ct);
        if (check.ErrorCode is not null)
            return Result<PaymentLinkDto>.Fail(check.ErrorCode, check.Message!);

        var token = await EnsureTokenAsync(payment, ct);
        return Result<PaymentLinkDto>.Ok(new PaymentLinkDto { Url = PayUrl(token) });
    }

    public async Task<Result<bool>> RefundAsync(int paymentId, CancellationToken ct = default)
    {
        var payment = await LoadClinicPaymentAsync(paymentId, ct);
        if (payment is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Cobrança não encontrada.");

        if (payment.Status != PaymentStatus.Paid || !payment.PaidOnline || payment.MpPaymentId is null)
            return Result<bool>.Fail(ErrorCodes.PaymentNotRefundable,
                "Só é possível reembolsar cobranças pagas online.");

        var connected = await accounts.GetConnectedAsync(payment.ClinicaId, ct);
        if (connected is null)
            return Result<bool>.Fail(ErrorCodes.MercadoPagoNotConnected,
                "Conecte o Mercado Pago para reembolsar cobranças.");

        try
        {
            await client.RefundAsync(connected.AccessToken, payment.MpPaymentId.Value,
                $"refund:{payment.Id}", ct);
        }
        catch (MercadoPagoApiException exception) when (exception.StatusCode == 401)
        {
            await accounts.MarkRevokedAsync(payment.ClinicaId, ct);
            return Result<bool>.Fail(ErrorCodes.MercadoPagoNotConnected,
                "A conexão com o Mercado Pago expirou. Reconecte a conta.");
        }
        catch (MercadoPagoApiException exception)
        {
            logger.LogWarning(exception, "Mercado Pago recusou o reembolso da cobrança {PaymentId}.", payment.Id);
            return Result<bool>.Fail(ErrorCodes.MercadoPagoRefundFailed, MercadoPagoMessage(exception));
        }

        payment.Status = PaymentStatus.Cancelled;
        payment.RefundedAt = time.GetUtcNow().UtcDateTime;
        payment.OnlineAlert = null;
        payment.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> RefundDuplicateAsync(int paymentId, CancellationToken ct = default)
    {
        var payment = await LoadClinicPaymentAsync(paymentId, ct);
        if (payment is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Cobrança não encontrada.");

        if (!TryParseDuplicateId(payment.OnlineAlert, out var duplicateMpPaymentId))
            return Result<bool>.Fail(ErrorCodes.PaymentNotRefundable,
                "Não há pagamento duplicado para reembolsar.");

        var connected = await accounts.GetConnectedAsync(payment.ClinicaId, ct);
        if (connected is null)
            return Result<bool>.Fail(ErrorCodes.MercadoPagoNotConnected,
                "Conecte o Mercado Pago para reembolsar cobranças.");

        try
        {
            await client.RefundAsync(connected.AccessToken, duplicateMpPaymentId,
                $"refund-duplicate:{payment.Id}:{duplicateMpPaymentId}", ct);
        }
        catch (MercadoPagoApiException exception) when (exception.StatusCode == 401)
        {
            await accounts.MarkRevokedAsync(payment.ClinicaId, ct);
            return Result<bool>.Fail(ErrorCodes.MercadoPagoNotConnected,
                "A conexão com o Mercado Pago expirou. Reconecte a conta.");
        }
        catch (MercadoPagoApiException exception)
        {
            logger.LogWarning(exception, "Mercado Pago recusou o reembolso duplicado da cobrança {PaymentId}.", payment.Id);
            return Result<bool>.Fail(ErrorCodes.MercadoPagoRefundFailed, MercadoPagoMessage(exception));
        }

        payment.OnlineAlert = null;
        payment.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private Task<Payment?> LoadClinicPaymentAsync(int paymentId, CancellationToken ct)
        => db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .Include(p => p.Clinica)
            .FirstOrDefaultAsync(p => p.Id == paymentId
                && p.ClinicaId == usuario.ClinicaId
                && !p.IsDeleted, ct);

    // Bloqueios comuns a /send e /link (o /send acrescenta o e-mail do paciente).
    private async Task<(string? ErrorCode, string? Message)> CheckSendableAsync(
        Payment payment, CancellationToken ct)
    {
        if (payment.Status != PaymentStatus.Pending)
            return (ErrorCodes.PaymentNotPending, "Só é possível enviar cobranças pendentes.");

        if (await accounts.GetConnectedAsync(payment.ClinicaId, ct) is null)
            return (ErrorCodes.MercadoPagoNotConnected, "Conecte o Mercado Pago para enviar cobranças.");

        return (null, null);
    }

    // ── Página pública ───────────────────────────────────────────────────────

    public async Task<Result<PublicPaymentDto>> GetPublicAsync(string token, CancellationToken ct = default)
    {
        var payment = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .Include(p => p.Clinica)
            .FirstOrDefaultAsync(p => p.PaymentLinkToken == token && !p.IsDeleted, ct);

        if (payment is null)
            return Result<PublicPaymentDto>.Fail(ErrorCodes.NotFound, "Link de pagamento inválido.");

        var connected = await accounts.GetConnectedAsync(payment.ClinicaId, ct);
        var status = payment.Status == PaymentStatus.Paid
            ? "Paid"
            : payment.Status == PaymentStatus.Pending && connected is not null
                ? "Pending"
                : "Unavailable";

        return Result<PublicPaymentDto>.Ok(new PublicPaymentDto
        {
            ClinicName = ClinicName(payment.Clinica),
            ClinicLogoUrl = payment.Clinica.LogoUrl,
            PatientFirstName = FirstName(payment.Patient.Name),
            Amount = payment.Amount,
            ReferenceMonth = payment.ReferenceMonth,
            PlanName = payment.Plan.Name,
            Status = status,
            MpStatus = payment.MpStatus,
            MpPaymentId = payment.MpPaymentId,
            PublicKey = connected?.Account.PublicKey ?? string.Empty,
            AcceptPix = payment.Clinica.OnlineAcceptPix,
            AcceptCredit = payment.Clinica.OnlineAcceptCredit,
            MaxInstallments = payment.Clinica.OnlineMaxInstallments,
            AcceptDebit = payment.Clinica.OnlineAcceptDebit,
        });
    }

    public async Task<Result<PublicPayResultDto>> PayAsync(
        string token, PublicPayRequest request, CancellationToken ct = default)
    {
        var payment = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .Include(p => p.Clinica)
            .FirstOrDefaultAsync(p => p.PaymentLinkToken == token && !p.IsDeleted, ct);

        if (payment is null)
            return Result<PublicPayResultDto>.Fail(ErrorCodes.NotFound, "Link de pagamento inválido.");

        if (payment.Status != PaymentStatus.Pending)
            return Result<PublicPayResultDto>.Fail(ErrorCodes.PaymentNotPending,
                "Esta cobrança não está mais disponível.");

        var now = time.GetUtcNow().UtcDateTime;
        if (payment.MpPaymentId is not null
            && payment.MpStatus is "pending" or "in_process"
            && payment.UpdatedAt > now.AddHours(-24))
        {
            return Result<PublicPayResultDto>.Fail(ErrorCodes.PaymentInProgress,
                "Já existe um pagamento em andamento para esta cobrança. Aguarde a confirmação.");
        }

        var connected = await accounts.GetConnectedAsync(payment.ClinicaId, ct);
        if (connected is null)
            return Result<PublicPayResultDto>.Fail(ErrorCodes.MercadoPagoNotConnected,
                "Esta cobrança não está mais disponível.");

        var method = request.PaymentMethodId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (method.Length == 0)
            return Result<PublicPayResultDto>.Fail(ErrorCodes.EmptyField, "Escolha uma forma de pagamento.");

        var type = request.PaymentTypeId?.Trim().ToLowerInvariant();
        var isPix = method == "pix" || type == "bank_transfer";
        var isDebit = !isPix && (type == "debit_card" || method.StartsWith("deb", StringComparison.Ordinal));
        var isCredit = !isPix && !isDebit;

        if (isPix && !payment.Clinica.OnlineAcceptPix)
            return Result<PublicPayResultDto>.Fail(ErrorCodes.PaymentMethodNotAllowed,
                "Esta clínica não aceita Pix.");
        if (isDebit && !payment.Clinica.OnlineAcceptDebit)
            return Result<PublicPayResultDto>.Fail(ErrorCodes.PaymentMethodNotAllowed,
                "Esta clínica não aceita cartão de débito.");
        if (isCredit && !payment.Clinica.OnlineAcceptCredit)
            return Result<PublicPayResultDto>.Fail(ErrorCodes.PaymentMethodNotAllowed,
                "Esta clínica não aceita cartão de crédito.");

        var installments = request.Installments ?? 1;
        if (isCredit && (installments < 1 || installments > payment.Clinica.OnlineMaxInstallments))
            return Result<PublicPayResultDto>.Fail(ErrorCodes.InstallmentsNotAllowed,
                $"Esta clínica aceita no máximo {payment.Clinica.OnlineMaxInstallments} parcela(s).");

        var payerEmail = request.Payer?.Email?.Trim();
        if (string.IsNullOrWhiteSpace(payerEmail))
            return Result<PublicPayResultDto>.Fail(ErrorCodes.EmptyField, "Informe o e-mail do pagador.");

        if (!isPix && string.IsNullOrWhiteSpace(request.Token))
            return Result<PublicPayResultDto>.Fail(ErrorCodes.EmptyField, "Token do cartão ausente.");

        var mpRequest = new MpPaymentRequest(
            TransactionAmount: payment.Amount, // sempre do banco, nunca do cliente
            Description: $"{payment.Plan.Name} — {MonthLabel(payment.ReferenceMonth)}",
            ExternalReference: $"payment:{payment.Id}",
            NotificationUrl: NotificationUrl(),
            PaymentMethodId: method,
            CardToken: isPix ? null : request.Token,
            Installments: isCredit ? installments : null,
            IssuerId: isPix ? null : request.IssuerId,
            PayerEmail: payerEmail,
            PayerIdentificationType: request.Payer?.Identification?.Type,
            PayerIdentificationNumber: request.Payer?.Identification?.Number,
            DateOfExpiration: isPix ? now.AddHours(24) : null);

        MpPayment created;
        try
        {
            created = await client.CreatePaymentAsync(connected.AccessToken, mpRequest,
                Guid.NewGuid().ToString(), ct);
        }
        catch (MercadoPagoApiException exception) when (exception.StatusCode == 401)
        {
            await accounts.MarkRevokedAsync(payment.ClinicaId, ct);
            return Result<PublicPayResultDto>.Fail(ErrorCodes.MercadoPagoNotConnected,
                "Esta cobrança não está mais disponível.");
        }
        catch (MercadoPagoApiException exception)
        {
            logger.LogWarning(exception, "Mercado Pago recusou o pagamento da cobrança {PaymentId}.", payment.Id);
            return Result<PublicPayResultDto>.Fail(ErrorCodes.MercadoPagoPaymentFailed,
                MercadoPagoMessage(exception));
        }

        var applied = await sync.ApplyAsync(payment.ClinicaId, created.Id, ct);
        return Result<PublicPayResultDto>.Ok(new PublicPayResultDto
        {
            PaymentId = created.Id,
            Status = applied?.MpStatus ?? created.Status,
        });
    }

    // ── Portal do paciente ───────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<PatientChargeDto>>> GetPatientChargesAsync(CancellationToken ct = default)
    {
        var payments = await db.Payments
            .Include(p => p.Plan)
            .Include(p => p.Clinica)
            .Where(p => p.Patient.PatientAccountId == loggedPatient.PatientAccountId
                && !p.IsDeleted
                && !p.Clinica.IsDeleted
                && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Paid)
                && db.ClinicMercadoPagoAccounts.Any(account =>
                    account.ClinicaId == p.ClinicaId && account.Status == MercadoPagoAccountStatus.Connected))
            .ToListAsync(ct);

        var ordered = payments
            .OrderBy(p => p.Status == PaymentStatus.Paid ? 1 : 0)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();

        var changed = false;
        foreach (var payment in ordered)
        {
            if (payment.PaymentLinkToken is null)
            {
                payment.PaymentLinkToken = NewToken();
                changed = true;
            }
        }
        if (changed)
            await db.SaveChangesAsync(ct);

        IReadOnlyList<PatientChargeDto> result = ordered.Select(payment => new PatientChargeDto
        {
            Id = payment.Id,
            ClinicName = ClinicName(payment.Clinica),
            Amount = payment.Amount,
            ReferenceMonth = payment.ReferenceMonth,
            PlanName = payment.Plan.Name,
            Status = payment.Status.ToString(),
            PayUrl = PayUrl(payment.PaymentLinkToken!),
        }).ToList();

        return Result<IReadOnlyList<PatientChargeDto>>.Ok(result);
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private async Task<string> EnsureTokenAsync(Payment payment, CancellationToken ct)
    {
        if (payment.PaymentLinkToken is not null)
            return payment.PaymentLinkToken;

        payment.PaymentLinkToken = NewToken();
        await db.SaveChangesAsync(ct);
        return payment.PaymentLinkToken;
    }

    private string PayUrl(string token) => $"{FrontendUrl}/pagar/{token}";

    // Deriva a URL pública do webhook do RedirectUri (mesmo host/scheme do callback).
    private string NotificationUrl()
    {
        var redirect = options.Value.RedirectUri;
        return Uri.TryCreate(redirect, UriKind.Absolute, out var callback)
            ? $"{callback.Scheme}://{callback.Authority}{WebhookPath}"
            : string.Empty;
    }

    private static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string ClinicName(Clinica clinic)
        => string.IsNullOrWhiteSpace(clinic.NomeFantasia) ? clinic.Nome : clinic.NomeFantasia;

    private static string FirstName(string? name)
        => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

    private static string MonthLabel(DateOnly referenceMonth)
    {
        var text = referenceMonth.ToString("MMMM 'de' yyyy", PtBr);
        return char.ToUpper(text[0], PtBr) + text[1..];
    }

    private static bool TryParseDuplicateId(string? alert, out long mpPaymentId)
    {
        const string prefix = "PagamentoDuplicado:";
        mpPaymentId = 0;
        return alert is not null
            && alert.StartsWith(prefix, StringComparison.Ordinal)
            && long.TryParse(alert[prefix.Length..], out mpPaymentId);
    }

    // A mensagem do Mercado Pago é mais útil que o corpo JSON cru.
    private static string MercadoPagoMessage(MercadoPagoApiException exception)
    {
        try
        {
            using var document = JsonDocument.Parse(exception.Body);
            if (document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(message.GetString()))
                return message.GetString()!;
        }
        catch (JsonException)
        {
            // Corpo não é JSON: cai no texto padrão.
        }

        return "Não foi possível concluir a operação no Mercado Pago. Tente novamente.";
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: "23505" };
}
