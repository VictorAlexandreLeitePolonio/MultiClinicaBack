namespace MultiClinica.API.Models;

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
    public int TipoSessaoId { get; set; }
    public Clinica Clinica { get; set; } = null!;
    public SessionType TipoSessao { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
}
