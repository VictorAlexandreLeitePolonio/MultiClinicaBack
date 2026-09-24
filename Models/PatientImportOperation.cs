namespace MultiClinica.API.Models;

public enum PatientImportStatus
{
    Processing,
    Completed
}

public class PatientImportOperation : AuditableEntity
{
    public Guid ImportId { get; set; } = Guid.NewGuid();
    public int ClinicaId { get; set; }
    public Clinica Clinica { get; set; } = null!;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public PatientImportStatus Status { get; set; } = PatientImportStatus.Processing;
    public string? ResponseJson { get; set; }
}
