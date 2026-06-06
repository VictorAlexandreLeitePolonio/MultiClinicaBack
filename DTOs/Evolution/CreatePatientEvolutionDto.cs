using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Evolution;

public class CreatePatientEvolutionDto
{
    public int? ProfessionalId { get; set; }
    public int? ServiceId { get; set; }
    public DateTime? Date { get; set; }
    public string? Description { get; set; }
    public string? Conduct { get; set; }
    public string? Observations { get; set; }
    public string? NextGuidance { get; set; }
    public EvolutionStatus Status { get; set; } = EvolutionStatus.Completed;
    public List<CreatePatientEvolutionValueDto> Values { get; set; } = [];
}
