namespace MultiClinica.API.Models;

public enum PatientEmailOutboxType
{
    ActivationInvitation,
    ClinicLinkNotice
}

public enum PatientEmailOutboxStatus
{
    Pending,
    Completed,
    Failed
}

public class PatientEmailOutbox : AuditableEntity
{
    public int ClinicaId { get; set; }
    public Clinica Clinica { get; set; } = null!;
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int PatientAccountId { get; set; }
    public PatientAccount PatientAccount { get; set; } = null!;
    public PatientEmailOutboxType Type { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public PatientEmailOutboxStatus Status { get; set; } = PatientEmailOutboxStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastError { get; set; }
}
