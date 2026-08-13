namespace MultiClinica.API.DTOs.Financial;

// Balanço operacional da clínica — dashboard, não um financeiro completo (ver plano de simplificação).
public sealed class FinancialBalanceDto
{
    public BalancePeriodDto Period { get; set; } = null!;
    public BalanceMoneySummaryDto Money { get; set; } = null!;
    public BalanceAppointmentsSummaryDto Appointments { get; set; } = null!;
    public BalancePatientsSummaryDto Patients { get; set; } = null!;
    public BalanceStockSummaryDto Stock { get; set; } = null!;
    public BalanceEvolutionSummaryDto Evolutions { get; set; } = null!;
    public List<BalanceRecentMovementDto> RecentMovements { get; set; } = [];
}
