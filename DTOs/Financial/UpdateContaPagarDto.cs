namespace MultiClinica.API.DTOs.Financial;

public class UpdateContaPagarDto
{
    public int? CategoriaFinanceiraId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal ValorOriginal { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorJuros { get; set; }
    public DateTime DataVencimento { get; set; }
    public string? Observacao { get; set; }
}
