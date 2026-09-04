using System.Net;
using System.Net.Mail;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

/// <summary>Envia e-mails via SMTP (System.Net.Mail). Configurado por SMTP_* no ambiente.</summary>
public class SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(options.From, options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.UseSsl,
            Credentials = string.IsNullOrEmpty(options.User)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(options.User, options.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("E-mail enviado para {To} (assunto: {Subject}).", to, subject);
    }
}

/// <summary>Fallback usado quando o SMTP não está configurado: apenas registra o e-mail no log.</summary>
public class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[LogEmailSender] SMTP não configurado — e-mail NÃO enviado.\n  Para: {To}\n  Assunto: {Subject}\n  Corpo:\n{Body}",
            to, subject, htmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>Opções de SMTP lidas da configuração/ambiente.</summary>
public class SmtpOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string? User { get; init; }
    public string? Password { get; init; }
    public string From { get; init; } = "no-reply@cliniqcare.com.br";
    public string FromName { get; init; } = "Cliniq Care";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

    public static SmtpOptions FromConfiguration(IConfiguration config) => new()
    {
        Host     = config["SMTP_HOST"] ?? string.Empty,
        Port     = int.TryParse(config["SMTP_PORT"], out var p) ? p : 587,
        UseSsl   = !bool.TryParse(config["SMTP_USE_SSL"], out var ssl) || ssl,
        User     = config["SMTP_USER"],
        Password = config["SMTP_PASSWORD"],
        From     = config["SMTP_FROM"] ?? "no-reply@cliniqcare.com.br",
        FromName = config["SMTP_FROM_NAME"] ?? "Cliniq Care",
    };
}
