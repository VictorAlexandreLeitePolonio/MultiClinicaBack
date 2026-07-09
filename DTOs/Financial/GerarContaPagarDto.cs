namespace MultiClinica.API.DTOs.Financial;

public class GerarContaPagarDto
{
    public DateTime DataVencimento { get; set; }
    public int? CategoriaFinanceiraId { get; set; }
}
