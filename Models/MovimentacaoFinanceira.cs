namespace MultiClinica.API.Models;

public enum TipoMovimentacaoFinanceira
{
    Entrada,
    Saida
}

public enum OrigemMovimentacaoFinanceira
{
    Recebimento,
    Estorno
}

public class MovimentacaoFinanceira : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int ContaFinanceiraId { get; set; }
    public int? CategoriaFinanceiraId { get; set; }
    public int? ContaReceberId { get; set; }
    public TipoMovimentacaoFinanceira Tipo { get; set; }
    public OrigemMovimentacaoFinanceira Origem { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime DataMovimentacao { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public ContaFinanceira ContaFinanceira { get; set; } = null!;
    public CategoriaFinanceira? CategoriaFinanceira { get; set; }
    public ContaReceber? ContaReceber { get; set; }
}
