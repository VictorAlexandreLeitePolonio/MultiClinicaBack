using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class MovimentacaoEstoqueResponseDto
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public TipoMovimentacaoEstoque Tipo { get; set; }
    public int Quantidade { get; set; }
    public int QuantidadeAnterior { get; set; }
    public int QuantidadeAtual { get; set; }
    public string? Origem { get; set; }
    public int? OrigemId { get; set; }
    public string? Observacao { get; set; }
    public bool IsCancelada { get; set; }
    public string? MotivoCancelamento { get; set; }
    public DateTime CreatedAt { get; set; }
}
