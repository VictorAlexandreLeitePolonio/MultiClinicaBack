using System.Net.Http.Json;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

/// <summary>
/// Envia e-mails pela API HTTP do Resend (https://api.resend.com/emails).
/// Usado quando RESEND_API_KEY está configurada — dispensa SMTP, que o Railway
/// bloqueia em alguns planos. Remetente vem de SMTP_FROM/SMTP_FROM_NAME.
/// </summary>
public class ResendEmailSender(HttpClient http, SmtpOptions options, ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("emails", new
        {
            from = $"{options.FromName} <{options.From}>",
            to = new[] { to },
            subject,
            html = htmlBody,
        }, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Resend retornou {(int)response.StatusCode}: {detail}");
        }

        logger.LogInformation("E-mail enviado via Resend para {To} (assunto: {Subject}).", to, subject);
    }
}
