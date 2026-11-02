using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Evolution;

// Série de um campo de ficha ao longo das evoluções concluídas do paciente.
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
