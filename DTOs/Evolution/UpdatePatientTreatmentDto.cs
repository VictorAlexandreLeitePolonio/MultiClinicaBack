using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Evolution;

public class UpdatePatientTreatmentDto
{
    public int? ProfessionalId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TreatmentStatus? Status { get; set; }
}
