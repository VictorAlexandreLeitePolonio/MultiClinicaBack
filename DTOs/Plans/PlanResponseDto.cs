namespace MultiClinica.API.DTOs.Plans;

using MultiClinica.API.Models;
public class PlanResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public TipoPlano TipoPlano { get; set; }
    public int TipoSessaoId { get; set; }
    public string TipoSessaoName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
