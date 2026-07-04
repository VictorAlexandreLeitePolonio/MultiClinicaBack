namespace MultiClinica.API.Models;

public class PagamentoContaPagar : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int ContaPagarId { get; set; }
    public int ContaFinanceiraId { get; set; }
    public int FormaPagamentoId { get; set; }
    public decimal Valor { get; set; }
    public DateTime DataPagamento { get; set; }
    public string? Observacao { get; set; }
    public bool IsEstornado { get; set; }
    public string? MotivoEstorno { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public ContaPagar ContaPagar { get; set; } = null!;
    public ContaFinanceira ContaFinanceira { get; set; } = null!;
    public FormaPagamento FormaPagamento { get; set; } = null!;
}
