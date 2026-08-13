namespace MultiClinica.API.DTOs.Financial;

public sealed class FinancialBalanceQueryDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
