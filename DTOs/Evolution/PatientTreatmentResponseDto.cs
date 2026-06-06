using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Evolution;

public class PatientTreatmentResponseDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? ProfessionalId { get; set; }
    public int TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TreatmentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
