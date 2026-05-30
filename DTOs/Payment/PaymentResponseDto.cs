namespace MultiClinica.API.DTOs.Payment;

using MultiClinica.API.Models;

// Dados retornados ao cliente nas respostas da API.
public class PaymentResponseDto
{
    public int Id { get; set; }
    public int ResponsavelId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal PlanAmount { get; set; }
    public string ReferenceMonth { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;

    public PaymentStatus Status              { get; set; }
    public DateTime?     PaidAt              { get; set; }
    public DateTime?     PaymentDate         { get; set; }
    public DateTime      CreatedAt           { get; set; }
}
