namespace MultiClinica.API.Models;

public enum EvolutionStatus
{
    Draft = 1,
    Completed = 2,
    Canceled = 3
}

public class PatientEvolution : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int PatientId { get; set; }
    public int TreatmentId { get; set; }
    public int ProfessionalId { get; set; }
    public int? ServiceId { get; set; }

    public DateTime Date { get; set; }

    public string? Description { get; set; }
    public string? Conduct { get; set; }
    public string? Observations { get; set; }
    public string? NextGuidance { get; set; }

    public EvolutionStatus Status { get; set; } = EvolutionStatus.Draft;

    public Patient Patient { get; set; } = null!;
    public PatientTreatment Treatment { get; set; } = null!;
    public User Professional { get; set; } = null!;
    public ICollection<PatientEvolutionValue> Values { get; set; } = [];
}
