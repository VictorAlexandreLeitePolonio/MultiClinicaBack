using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Evolution;

public class CreateEvolutionTemplateFieldDto
{
    public string Label { get; set; } = string.Empty;
    public EvolutionFieldType Type { get; set; }
    public EvolutionFieldUnit? Unit { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public decimal? TargetValue { get; set; }
    public EvolutionDirection ExpectedDirection { get; set; } = EvolutionDirection.Neutral;
    public decimal Weight { get; set; } = 1;
    public bool Required { get; set; }
    public bool ShowInChart { get; set; } = true;
    public int Order { get; set; }
    public string? OptionsJson { get; set; }
}
