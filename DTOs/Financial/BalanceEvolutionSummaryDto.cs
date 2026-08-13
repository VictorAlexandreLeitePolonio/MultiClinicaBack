namespace MultiClinica.API.DTOs.Financial;

public sealed class BalanceEvolutionSummaryDto
{
    public int EvolutionsInPeriod { get; set; }
    public int TreatmentsInProgress { get; set; }
    public int CompletedTreatments { get; set; }
}
