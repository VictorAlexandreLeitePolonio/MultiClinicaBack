namespace MultiClinica.API.DTOs.Financial;

public class AbrirCaixaDto
{
    public int ContaFinanceiraId { get; set; }
    public decimal SaldoInicial { get; set; }
    public string? Observacao { get; set; }
}
