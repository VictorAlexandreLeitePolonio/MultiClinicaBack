namespace MultiClinica.API.DTOs.Evolution;

public class CreateEvolutionTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsDefault { get; set; }
}
