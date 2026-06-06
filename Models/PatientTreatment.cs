namespace MultiClinica.API.Models;

public enum TreatmentStatus
{
    Active = 1,
    Paused = 2,
    Completed = 3,
    Canceled = 4
}

public class PatientTreatment : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int PatientId { get; set; }
    public int? ProfessionalId { get; set; }
    public int TemplateId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public TreatmentStatus Status { get; set; } = TreatmentStatus.Active;

    public Patient Patient { get; set; } = null!;
    public User? Professional { get; set; }
    public EvolutionTemplate Template { get; set; } = null!;
    public ICollection<PatientEvolution> Evolutions { get; set; } = [];
}
