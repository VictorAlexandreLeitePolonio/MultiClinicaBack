namespace MultiClinica.API.Models;

// Uso mensal de uma métrica de cota. Chave: (ClinicaId, Period, Metric).
public class ClinicUsage
{
    public int ClinicaId { get; set; }
    public string Period { get; set; } = string.Empty; // "yyyy-MM" em America/Sao_Paulo
    public UsageMetric Metric { get; set; }
    public int Used { get; set; }
    public int ExtraGranted { get; set; }
}
