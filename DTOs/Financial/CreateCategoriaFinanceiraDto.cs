using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class CreateCategoriaFinanceiraDto
{
    public string Nome { get; set; } = string.Empty;
    public TipoCategoriaFinanceira Tipo { get; set; }
}
