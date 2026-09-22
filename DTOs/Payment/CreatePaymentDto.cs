namespace MultiClinica.API.DTOs.Payment;

using MultiClinica.API.Models;

public class CreatePaymentDto
{
    public int ResponsavelId { get; set; }
    public int PatientId { get; set; }
    public int PlanId { get; set; }
    public DateOnly ReferenceMonth { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateOnly? PaidAt { get; set; }
    public DateOnly? PaymentDate { get; set; }
}
