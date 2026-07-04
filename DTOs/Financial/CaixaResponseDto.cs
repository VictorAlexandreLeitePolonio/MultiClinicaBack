using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class CaixaResponseDto
{
    public int Id { get; set; }
    public int ContaFinanceiraId { get; set; }
    public DateTime DataAbertura { get; set; }
    public DateTime? DataFechamento { get; set; }
    public decimal SaldoInicial { get; set; }
    public decimal? SaldoFinalInformado { get; set; }
    public decimal? SaldoFinalCalculado { get; set; }
    public decimal? Diferenca { get; set; }
    public StatusCaixa Status { get; set; }
    public string? Observacao { get; set; }
    public DateTime CreatedAt { get; set; }
}
