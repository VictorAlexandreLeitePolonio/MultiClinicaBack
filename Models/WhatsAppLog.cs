namespace ProjetoLP.API.Models;

// Registra cada tentativa de envio de mensagem via WhatsApp —
// tanto lembretes de consulta quanto lembretes de vencimento de pagamento.
public class WhatsAppLog
{
    public int Id { get; set; }

    // Paciente destinatário — nullable pois futuramente pode haver envios sem vínculo de paciente.
    public int? PatientId { get; set; }

    public string Phone   { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // Tipo de lembrete: "AppointmentReminder" ou "PaymentReminder".
    public string Type { get; set; } = string.Empty;

    // Indica se a Evolution API retornou sucesso.
    public bool    Success      { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Patient? Patient { get; set; }
}
