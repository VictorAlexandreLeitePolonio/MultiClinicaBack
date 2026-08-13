namespace MultiClinica.API.Models;

public enum TipoMovimentacaoEstoque
{
    Entrada,
    Saida,
    Ajuste,
    Perda,
    UsoInterno,
    Venda,
    Compra
}

public class MovimentacaoEstoque
{
    public int Id { get; set; }
    public int ClinicaId { get; set; }
    public int ProdutoId { get; set; }
    public TipoMovimentacaoEstoque Tipo { get; set; }
    public int Quantidade { get; set; }
    public int QuantidadeAnterior { get; set; }
    public int QuantidadeAtual { get; set; }
    public decimal? UnitValue { get; set; }
    public decimal? TotalValue { get; set; }
    public string? Origem { get; set; }
    public int? OrigemId { get; set; }
    public string? Observacao { get; set; }
    public int UsuarioId { get; set; }
    public bool IsCancelada { get; set; }
    public string? MotivoCancelamento { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Clinica Clinica { get; set; } = null!;
    public Produto Produto { get; set; } = null!;
}
