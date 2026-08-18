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
