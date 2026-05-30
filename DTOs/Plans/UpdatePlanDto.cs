namespace ProjetoLP.API.DTOs.Plans;

using ProjetoLP.API.Models;
public class UpdatePlanDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public TipoPlano TipoPlano { get; set; }
    public TipoSessao TipoSessao { get; set; }
}