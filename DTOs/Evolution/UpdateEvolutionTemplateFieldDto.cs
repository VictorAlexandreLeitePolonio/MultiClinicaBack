using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Evolution;

public class UpdateEvolutionTemplateFieldDto
{
    public string? Label { get; set; }
    public EvolutionFieldType? Type { get; set; }
    public EvolutionFieldUnit? Unit { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public decimal? TargetValue { get; set; }
    public EvolutionDirection? ExpectedDirection { get; set; }
    public decimal? Weight { get; set; }
    public bool? Required { get; set; }
    public bool? ShowInChart { get; set; }
    public bool? IsActive { get; set; }
    public int? Order { get; set; }
    public string? OptionsJson { get; set; }
}
