using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class CategoriaFinanceiraResponseDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TipoCategoriaFinanceira Tipo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
