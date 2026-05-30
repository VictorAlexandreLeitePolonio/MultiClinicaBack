namespace MultiClinica.API.Models;

public enum ClinicChargeStatus
{
    Pending,
    Paid,
    Cancelled
}

public class ClinicCharge : AuditableEntity
{
    public int ClinicaId { get; set; }
    public string ReferenceMonth { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public ClinicChargeStatus Status { get; set; } = ClinicChargeStatus.Pending;
    public DateTime? PaidAt { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public Clinica Clinica { get; set; } = null!;
}
