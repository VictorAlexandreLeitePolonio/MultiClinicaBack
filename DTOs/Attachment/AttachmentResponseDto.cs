using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Attachment;

public class AttachmentResponseDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? MedicalRecordId { get; set; }
    public ClinicalAttachmentType Type { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
}
