namespace MultiClinica.API.DTOs.Financial;

// Balanço operacional mensal — indicadores de atividade da clínica (sem dados financeiros).
public class FinancialBalanceDto
{
    // Mês de referência no formato "MM-YYYY".
    public string ReferenceMonth { get; set; } = string.Empty;

    public int ActivePatients { get; set; }
    public int NewPatients { get; set; }
    public int ScheduledAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public int CompletedEvolutions { get; set; }
    public int LowStockProducts { get; set; }
    public int StockMovements { get; set; }
}
