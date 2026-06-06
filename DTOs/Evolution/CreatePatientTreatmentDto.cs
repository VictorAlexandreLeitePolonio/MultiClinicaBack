namespace MultiClinica.API.DTOs.Evolution;

public class CreatePatientTreatmentDto
{
    public int TemplateId { get; set; }
    public int? ProfessionalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartedAt { get; set; }
}
