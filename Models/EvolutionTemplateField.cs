namespace MultiClinica.API.Models;

public enum EvolutionFieldType
{
    Number = 1,
    Scale = 2,
    Percentage = 3,
    Boolean = 4,
    Text = 5,
    SelectScore = 6
}

public enum EvolutionFieldUnit
{
    None = 0,
    Points = 1,
    Percentage = 2,
    Kg = 3,
    G = 4,
    Mg = 5,
    Cm = 6,
    Mm = 7,
    M = 8,
    Degrees = 9,
    Seconds = 10,
    Minutes = 11,
    Hours = 12,
    Days = 13,
    Repetitions = 14,
    Liters = 15,
    Ml = 16,
    Score = 17
}

public enum EvolutionDirection
{
    Neutral = 0,
    Increase = 1,
    Decrease = 2
}

public class EvolutionTemplateField : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int TemplateId { get; set; }

    public string Label { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;

    public EvolutionFieldType Type { get; set; }
    public EvolutionFieldUnit Unit { get; set; } = EvolutionFieldUnit.None;

    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public decimal? TargetValue { get; set; }

    public EvolutionDirection ExpectedDirection { get; set; } = EvolutionDirection.Neutral;

    public decimal Weight { get; set; } = 1;
    public bool Required { get; set; }
    public bool ShowInChart { get; set; } = true;
    public int Order { get; set; }

    public string? OptionsJson { get; set; }

    public EvolutionTemplate Template { get; set; } = null!;
    public ICollection<PatientEvolutionValue> Values { get; set; } = [];
}
