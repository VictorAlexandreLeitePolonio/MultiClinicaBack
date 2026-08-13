namespace MultiClinica.API.DTOs.Financial;

public sealed class BalancePatientsSummaryDto
{
    public int Active { get; set; }
    public int NewInPeriod { get; set; }
    public int Total { get; set; }
}
