using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class ContaFinanceiraResponseDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TipoContaFinanceira Tipo { get; set; }
    public decimal SaldoInicial { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
