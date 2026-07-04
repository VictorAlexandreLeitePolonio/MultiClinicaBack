namespace MultiClinica.API.DTOs.Financial;

public class RecebimentoResponseDto
{
    public int Id { get; set; }
    public int ContaReceberId { get; set; }
    public int ContaFinanceiraId { get; set; }
    public int FormaPagamentoId { get; set; }
    public decimal Valor { get; set; }
    public DateTime DataRecebimento { get; set; }
    public string? Observacao { get; set; }
    public bool IsEstornado { get; set; }
    public DateTime CreatedAt { get; set; }
}
