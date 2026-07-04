namespace MultiClinica.API.DTOs.Financial;

public class PagamentoContaPagarResponseDto
{
    public int Id { get; set; }
    public int ContaPagarId { get; set; }
    public int ContaFinanceiraId { get; set; }
    public int FormaPagamentoId { get; set; }
    public decimal Valor { get; set; }
    public DateTime DataPagamento { get; set; }
    public string? Observacao { get; set; }
    public bool IsEstornado { get; set; }
    public DateTime CreatedAt { get; set; }
}
