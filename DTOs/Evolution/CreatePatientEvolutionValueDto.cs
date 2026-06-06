namespace MultiClinica.API.DTOs.Evolution;

public class CreatePatientEvolutionValueDto
{
    public int FieldId { get; set; }
    public decimal? ValueNumber { get; set; }
    public string? ValueText { get; set; }
    public bool? ValueBoolean { get; set; }
    public string? ValueJson { get; set; }
}
