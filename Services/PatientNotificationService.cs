using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class PatientNotificationService(
    IPatientTokenService tokenService,
    IEmailSender emailSender,
    IConfiguration config,
    ILogger<PatientNotificationService> logger) : IPatientNotificationService
{
    private static readonly TimeSpan ActivationTtl = TimeSpan.FromHours(72);
    private static readonly TimeSpan ResetTtl = TimeSpan.FromHours(1);

    private string FrontendUrl => (config["FRONTEND_URL"] ?? "http://localhost:3000").TrimEnd('/');

    public async Task<bool> SendActivationInviteAsync(PatientAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.Email))
            return false;

        var token = await tokenService.IssueAsync(account.Id, PatientAuthTokenType.Activation, ActivationTtl);
        var link = $"{FrontendUrl}/ativar-conta?token={token}";
        var body = $"""
            <p>Olá{NamePart(account)},</p>
            <p>Você recebeu acesso ao portal do paciente. Clique no link abaixo para ativar sua conta e definir sua senha:</p>
            <p><a href="{link}">Ativar minha conta</a></p>
            <p>Este link expira em 72 horas.</p>
            """;

        return await TrySendAsync(account.Email, "Ative seu acesso ao portal do paciente", body);
    }

    public async Task<bool> SendNewLinkNoticeAsync(PatientAccount account, string clinicName)
    {
        if (string.IsNullOrWhiteSpace(account.Email))
            return false;

        var link = $"{FrontendUrl}/login";
        var body = $"""
            <p>Olá{NamePart(account)},</p>
            <p>Sua conta foi vinculada à clínica <strong>{clinicName}</strong>.</p>
            <p>Acesse o portal do paciente para acompanhar suas consultas:</p>
            <p><a href="{link}">Acessar o portal</a></p>
            """;

        return await TrySendAsync(account.Email, "Você foi vinculado a uma nova clínica", body);
    }

    public async Task<bool> SendPasswordResetAsync(PatientAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.Email))
            return false;

        var token = await tokenService.IssueAsync(account.Id, PatientAuthTokenType.PasswordReset, ResetTtl);
        var link = $"{FrontendUrl}/redefinir-senha?token={token}";
        var body = $"""
            <p>Olá{NamePart(account)},</p>
            <p>Recebemos um pedido para redefinir sua senha. Clique no link abaixo:</p>
            <p><a href="{link}">Redefinir minha senha</a></p>
            <p>Este link expira em 1 hora. Se não foi você, ignore este e-mail.</p>
            """;

        return await TrySendAsync(account.Email, "Redefinição de senha", body);
    }

    // ── Solicitações de consulta (BACK-4) ────────────────────────────────────

    public async Task NotifyRequestCreatedAsync(Clinica clinic, PatientAccount account, AppointmentRequest request)
    {
        var to = ClinicEmail(clinic);
        if (to is null) return;

        var body = $"""
            <p>Nova solicitação de consulta de <strong>{account.Name ?? account.Email}</strong>.</p>
            <p>Data desejada: {request.RequestedDate:dd/MM/yyyy HH:mm}</p>
            <p>Motivo: {request.Reason ?? "-"}</p>
            """;
        await TrySendAsync(to, "Nova solicitação de consulta", body);
    }

    public async Task NotifyRequestAcceptedAsync(PatientAccount account, Clinica clinic, AppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(account.Email)) return;
        var body = $"""
            <p>Olá{NamePart(account)},</p>
            <p>Sua solicitação de consulta na clínica <strong>{ClinicName(clinic)}</strong> foi <strong>aceita</strong>.</p>
            <p>Data: {request.RequestedDate:dd/MM/yyyy HH:mm}</p>
            """;
        await TrySendAsync(account.Email, "Sua consulta foi confirmada", body);
    }

    public async Task NotifyRequestRejectedAsync(PatientAccount account, Clinica clinic, AppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(account.Email)) return;
        var body = $"""
            <p>Olá{NamePart(account)},</p>
            <p>Sua solicitação de consulta na clínica <strong>{ClinicName(clinic)}</strong> foi recusada.</p>
            <p>Motivo: {request.ResponseReason ?? "-"}</p>
            """;
        await TrySendAsync(account.Email, "Solicitação de consulta recusada", body);
    }

    public async Task NotifyRequestCancelledAsync(PatientAccount account, Clinica clinic, AppointmentRequest request)
    {
        // Cancelamento pelo paciente → avisa a clínica; pela clínica → avisa o paciente.
        if (request.CancelledBy == CancellationOrigin.Patient)
        {
            var to = ClinicEmail(clinic);
            if (to is null) return;
            var body = $"""
                <p>O paciente <strong>{account.Name ?? account.Email}</strong> cancelou uma solicitação de consulta.</p>
                <p>Data: {request.RequestedDate:dd/MM/yyyy HH:mm}</p>
                <p>Motivo: {request.ResponseReason ?? "-"}</p>
                """;
            await TrySendAsync(to, "Solicitação de consulta cancelada pelo paciente", body);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(account.Email)) return;
            var body = $"""
                <p>Olá{NamePart(account)},</p>
                <p>A clínica <strong>{ClinicName(clinic)}</strong> cancelou sua solicitação de consulta.</p>
                <p>Motivo: {request.ResponseReason ?? "-"}</p>
                """;
            await TrySendAsync(account.Email, "Solicitação de consulta cancelada", body);
        }
    }

    private static string? ClinicEmail(Clinica clinic)
        => !string.IsNullOrWhiteSpace(clinic.ContactEmail) ? clinic.ContactEmail
         : !string.IsNullOrWhiteSpace(clinic.Email) ? clinic.Email
         : null;

    private static string ClinicName(Clinica clinic)
        => string.IsNullOrWhiteSpace(clinic.NomeFantasia) ? clinic.Nome : clinic.NomeFantasia;

    private static string NamePart(PatientAccount account)
        => string.IsNullOrWhiteSpace(account.Name) ? "" : $" {account.Name}";

    private async Task<bool> TrySendAsync(string to, string subject, string body)
    {
        try
        {
            await emailSender.SendAsync(to, subject, body);
            return true;
        }
        catch (Exception ex)
        {
            // Falha de envio nunca reverte cadastro/vínculo já persistido.
            logger.LogError(ex, "Falha ao enviar e-mail para {To} (assunto: {Subject}).", to, subject);
            return false;
        }
    }
}
