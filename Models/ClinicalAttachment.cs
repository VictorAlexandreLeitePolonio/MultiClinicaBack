namespace MultiClinica.API.Models;

public enum ClinicalAttachmentType
{
    Contract,
    Exam,
    Other
}

public class ClinicalAttachment : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int PatientId { get; set; }
    public int? MedicalRecordId { get; set; }
    public ClinicalAttachmentType Type { get; set; } = ClinicalAttachmentType.Other;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Clinica Clinica { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public MedicalRecord? MedicalRecord { get; set; }
    public User UploadedByUser { get; set; } = null!;
}
