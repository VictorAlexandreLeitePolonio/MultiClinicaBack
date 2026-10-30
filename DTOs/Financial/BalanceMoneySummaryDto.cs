namespace MultiClinica.API.DTOs.Financial;

public sealed class BalanceMoneySummaryDto
{
    public decimal AppointmentIncome { get; set; }
    public decimal TotalIncome { get; set; }

    public decimal ManualExpenseCost { get; set; }
    public decimal TotalOutcome { get; set; }

    public decimal EstimatedProfit { get; set; }

    public int PaidAppointmentCount { get; set; }
    public int ManualExpenseCount { get; set; }
}
