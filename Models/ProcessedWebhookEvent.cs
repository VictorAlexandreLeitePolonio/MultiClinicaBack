namespace MultiClinica.API.Models;

// Idempotência de webhooks: o mesmo evento nunca é aplicado duas vezes.
public class ProcessedWebhookEvent
{
    public string Provider { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
