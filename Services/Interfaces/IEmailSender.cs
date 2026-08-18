namespace MultiClinica.API.Services.Interfaces;

public interface IEmailSender
{
    /// <summary>Envia um e-mail HTML. Lança em caso de falha de transporte.</summary>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
