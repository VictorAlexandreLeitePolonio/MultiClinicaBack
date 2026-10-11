namespace MultiClinica.API.Models;

public enum AgentChannel { Telegram, WhatsApp }
public enum AgentProfile { Owner, Patient }

// Uma conversa por pessoa por canal. ClinicaId/UserId/PatientId são sempre resolvidos pelo código.
public class AgentConversation : AuditableEntity
{
    public int ClinicaId { get; set; }
    public AgentChannel Channel { get; set; }
    public AgentProfile Profile { get; set; }
    public string ExternalChatId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public int? PatientId { get; set; }
    public int? AppointmentRequestId { get; set; }
    public DateTime LastInboundAt { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public ICollection<AgentMessage> Messages { get; set; } = [];
}
