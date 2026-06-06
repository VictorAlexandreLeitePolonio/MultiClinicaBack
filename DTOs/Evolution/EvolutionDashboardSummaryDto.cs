namespace MultiClinica.API.DTOs.Evolution;

public class EvolutionDashboardSummaryDto
{
    public int ActiveTreatments { get; set; }
    public int CompletedEvolutionsThisMonth { get; set; }
    public int PatientsImproving { get; set; }
    public int PatientsStable { get; set; }
    public int PatientsWorsening { get; set; }
    public decimal? AverageProgress { get; set; }
    public List<MostUsedEvolutionTemplateDto> MostUsedTemplates { get; set; } = [];
}

public class MostUsedEvolutionTemplateDto
{
    public int TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
