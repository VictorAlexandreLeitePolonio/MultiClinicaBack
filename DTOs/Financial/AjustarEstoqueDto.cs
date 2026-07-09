namespace MultiClinica.API.DTOs.Financial;

public class AjustarEstoqueDto
{
    public int ProdutoId { get; set; }
    public int NovaQuantidade { get; set; }
    public string Observacao { get; set; } = string.Empty;
}
