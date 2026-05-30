namespace ProjetoLP.API.DTOs.Plans;

using ProjetoLP.API.Models;
public class PlanResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public TipoPlano TipoPlano { get; set; }
    public TipoSessao TipoSessao { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}