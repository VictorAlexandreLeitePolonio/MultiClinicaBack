namespace MultiClinica.API.Models;

public class EvolutionTemplate : AuditableEntity
{
    public int ClinicaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsDefault { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public ICollection<EvolutionTemplateField> Fields { get; set; } = [];
    public ICollection<PatientTreatment> Treatments { get; set; } = [];
}
