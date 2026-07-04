namespace MultiClinica.API.Models;

public enum TipoContaFinanceira
{
    Caixa,
    Banco,
    Cartao,
    Outro
}

public class ContaFinanceira : AuditableEntity
{
    public int ClinicaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TipoContaFinanceira Tipo { get; set; }
    public decimal SaldoInicial { get; set; }

    public Clinica Clinica { get; set; } = null!;
}
