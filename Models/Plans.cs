namespace ProjetoLP.API.Models;

public enum TipoSessao
{
    Fisioterapia,
    Pilates,
    Massagem,
    Hidrolipo,
    Lipedema,
    Linfedema,
}
public enum TipoPlano
{
    Mensal,
    Avulso
}
public class Plans
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public TipoPlano TipoPlano { get; set; }
    public TipoSessao TipoSessao { get; set; }
    // Planos inativos não aparecem para seleção em novos pagamentos.
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Payment> Payments { get; set; } = [];
}

