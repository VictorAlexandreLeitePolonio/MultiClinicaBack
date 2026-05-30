namespace ProjetoLP.API.DTOs.Payment;

// Dados recebidos na criação de um pagamento.
// Status não está aqui — todo pagamento começa como Pending automaticamente.
public class CreatePaymentDto
{
    public int UserId { get; set; }
    public int PatientId { get; set; }
    public int PlanId { get; set; }
    public string    ReferenceMonth { get; set; } = string.Empty; // Formato: "YYYY-MM"
    public string    PaymentMethod  { get; set; } = string.Empty;
    public DateTime? PaymentDate    { get; set; } // Data de vencimento — dispara lembrete 24h antes.
}
