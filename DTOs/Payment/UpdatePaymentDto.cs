namespace ProjetoLP.API.DTOs.Payment;

using ProjetoLP.API.Models;

// Dados permitidos na atualização de um pagamento.
public class UpdatePaymentDto
{
    public string ReferenceMonth { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public int PlanId { get; set; }
    public PaymentStatus Status      { get; set; } = PaymentStatus.Pending;
    public DateTime?     PaidAt      { get; set; } // Preenchido quando Status = Paid, null nos demais casos.
    public DateTime?     PaymentDate { get; set; } // Data de vencimento — dispara lembrete 24h antes.
}
