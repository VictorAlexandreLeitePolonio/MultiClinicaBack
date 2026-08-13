namespace MultiClinica.API.Models;

public class ClinicExpense : AuditableEntity
{
    public int ClinicaId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }

    public Clinica Clinica { get; set; } = null!;
}
