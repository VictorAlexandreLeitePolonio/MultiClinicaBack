namespace MultiClinica.API.DTOs.Payment;

using MultiClinica.API.Models;

// Dados permitidos na atualização de um pagamento.
public class UpdatePaymentDto
{
    public DateOnly ReferenceMonth { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public int PlanId { get; set; }
    public PaymentStatus Status      { get; set; } = PaymentStatus.Pending;
    public DateOnly?     PaidAt      { get; set; }
    public DateOnly?     PaymentDate { get; set; }
}
