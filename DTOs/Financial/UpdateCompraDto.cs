namespace MultiClinica.API.DTOs.Financial;

public class UpdateCompraDto
{
    public int FornecedorId { get; set; }
    public DateTime DataCompra { get; set; }
    public string? Observacao { get; set; }
    public List<CompraItemDto> Itens { get; set; } = [];
}
