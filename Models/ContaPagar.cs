namespace MultiClinica.API.Models;

public enum StatusContaPagar
{
    Aberta,
    Parcial,
    Paga,
    Cancelada
}

public enum OrigemContaPagar
{
    Manual,
    Compra,
    DespesaRecorrente,
    Outro
}

public class ContaPagar : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int FornecedorId { get; set; }
    public int? CategoriaFinanceiraId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal ValorOriginal { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorJuros { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorPago { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime DataVencimento { get; set; }
    public DateTime? DataPagamento { get; set; }
    public StatusContaPagar Status { get; set; } = StatusContaPagar.Aberta;
    public OrigemContaPagar Origem { get; set; } = OrigemContaPagar.Manual;
    public int? OrigemId { get; set; }
    public string? Observacao { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public Fornecedor Fornecedor { get; set; } = null!;
    public CategoriaFinanceira? CategoriaFinanceira { get; set; }
    public ICollection<PagamentoContaPagar> Pagamentos { get; set; } = [];
}
