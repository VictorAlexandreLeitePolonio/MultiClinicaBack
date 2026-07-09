namespace MultiClinica.API.DTOs.Financial;

public class CompraItemResponseDto
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
    public decimal? ValorUnitario { get; set; }
    public decimal? ValorTotal { get; set; }
}
