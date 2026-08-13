namespace MultiClinica.API.DTOs.Financial;

public sealed class BalanceAppointmentsSummaryDto
{
    public int Scheduled { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int NoShow { get; set; }
    public int Total { get; set; }
}
