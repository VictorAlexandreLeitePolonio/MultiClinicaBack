namespace MultiClinica.API.DTOs.Financial;

public sealed class UpdateClinicExpenseDto
{
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
}
