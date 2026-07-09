namespace MultiClinica.API.Models;

public class Produto : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int? CategoriaProdutoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? CodigoInterno { get; set; }
    public string? CodigoBarras { get; set; }
    public decimal ValorCompra { get; set; }
    public decimal ValorVenda { get; set; }
    public int QuantidadeAtual { get; set; }
    public int QuantidadeMinima { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public CategoriaProduto? CategoriaProduto { get; set; }
}
