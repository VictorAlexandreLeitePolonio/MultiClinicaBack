namespace MultiClinica.API.DTOs.Financial;

public class ProdutoAlertaDto
{
    public int ProdutoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeAtual { get; set; }
    public int QuantidadeMinima { get; set; }
}
