using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class UpdateContaFinanceiraDto
{
    public string Nome { get; set; } = string.Empty;
    public TipoContaFinanceira Tipo { get; set; }
    public decimal SaldoInicial { get; set; }
}
