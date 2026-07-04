using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class UpdateCategoriaFinanceiraDto
{
    public string Nome { get; set; } = string.Empty;
    public TipoCategoriaFinanceira Tipo { get; set; }
}
