namespace MultiClinica.API.DTOs.Financial;

public sealed class ClinicExpenseResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
