namespace MultiClinica.API.DTOs.Financial;

public class ProdutoMovimentadoDto
{
    public int ProdutoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeMovimentada { get; set; }
}
