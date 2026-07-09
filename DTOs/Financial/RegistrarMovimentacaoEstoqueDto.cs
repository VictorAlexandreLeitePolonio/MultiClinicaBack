namespace MultiClinica.API.DTOs.Financial;

public class RegistrarMovimentacaoEstoqueDto
{
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
    public string? Observacao { get; set; }
}
