namespace MultiClinica.API.Models;

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
public class Plans : AuditableEntity
{
    public int ClinicaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public TipoPlano TipoPlano { get; set; }
    public TipoSessao TipoSessao { get; set; }
    public Clinica Clinica { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
}
