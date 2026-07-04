using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class CreateContaFinanceiraDto
{
    public string Nome { get; set; } = string.Empty;
    public TipoContaFinanceira Tipo { get; set; }
    public decimal SaldoInicial { get; set; }
}
