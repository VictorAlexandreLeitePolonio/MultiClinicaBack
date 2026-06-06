using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Evolution;

public class TreatmentProgressResponseDto
{
    public TreatmentProgressTreatmentDto Treatment { get; set; } = new();
    public TreatmentProgressSummaryDto Summary { get; set; } = new();
    public List<TreatmentProgressChartDto> Charts { get; set; } = [];
}

public class TreatmentProgressTreatmentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class TreatmentProgressSummaryDto
{
    public int TotalEvolutions { get; set; }
    public decimal? OverallProgress { get; set; }
    public int ImprovingFields { get; set; }
    public int WorseningFields { get; set; }
    public int StableFields { get; set; }
    public DateTime? LastEvolutionDate { get; set; }
}

public class TreatmentProgressChartDto
{
    public int FieldId { get; set; }
    public string Label { get; set; } = string.Empty;
    public EvolutionFieldUnit Unit { get; set; }
    public EvolutionDirection Direction { get; set; }
    public decimal InitialValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? Progress { get; set; }
    public List<TreatmentProgressPointDto> Data { get; set; } = [];
}

public class TreatmentProgressPointDto
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
}
