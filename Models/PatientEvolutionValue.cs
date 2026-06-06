namespace MultiClinica.API.Models;

public class PatientEvolutionValue : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int EvolutionId { get; set; }
    public int FieldId { get; set; }

    public decimal? ValueNumber { get; set; }
    public string? ValueText { get; set; }
    public bool? ValueBoolean { get; set; }
    public string? ValueJson { get; set; }

    public PatientEvolution Evolution { get; set; } = null!;
    public EvolutionTemplateField Field { get; set; } = null!;
}
