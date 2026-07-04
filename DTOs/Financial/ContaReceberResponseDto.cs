using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class ContaReceberResponseDto
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public int? CategoriaFinanceiraId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal ValorOriginal { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorJuros { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorRecebido { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime DataVencimento { get; set; }
    public DateTime? DataPagamento { get; set; }
    public StatusContaReceber Status { get; set; }
    public bool Vencida { get; set; }
    public OrigemContaReceber Origem { get; set; }
    public string? Observacao { get; set; }
    public DateTime CreatedAt { get; set; }
}
