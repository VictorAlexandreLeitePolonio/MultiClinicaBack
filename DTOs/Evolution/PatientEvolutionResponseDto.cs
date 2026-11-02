using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Evolution;

public class PatientEvolutionResponseDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? AppointmentId { get; set; }
    // Fichas medidas nesta evolução (nomes, na ordem dos campos).
    public List<string> TemplateNames { get; set; } = [];
    public int ProfessionalId { get; set; }
    public int? ServiceId { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? Conduct { get; set; }
    public string? Observations { get; set; }
    public string? NextGuidance { get; set; }
    public EvolutionStatus Status { get; set; }
    public List<PatientEvolutionValueResponseDto> Values { get; set; } = [];
}
