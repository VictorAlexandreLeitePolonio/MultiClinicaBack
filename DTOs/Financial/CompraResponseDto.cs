using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class CompraResponseDto
{
    public int Id { get; set; }
    public int FornecedorId { get; set; }
    public DateTime DataCompra { get; set; }
    public decimal? ValorTotal { get; set; }
    public StatusCompra Status { get; set; }
    public string? Observacao { get; set; }
    public List<CompraItemResponseDto> Itens { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
