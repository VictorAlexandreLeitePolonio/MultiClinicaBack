namespace MultiClinica.API.Models;

public enum StatusCompra
{
    Aberta,
    Aprovada,
    Recebida,
    Cancelada
}

public class Compra : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int FornecedorId { get; set; }
    public DateTime DataCompra { get; set; }
    public decimal ValorTotal { get; set; }
    public StatusCompra Status { get; set; } = StatusCompra.Aberta;
    public string? Observacao { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public Fornecedor Fornecedor { get; set; } = null!;
    public ICollection<CompraItem> Itens { get; set; } = [];
}

public class CompraItem : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int CompraId { get; set; }
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }

    public Compra Compra { get; set; } = null!;
    public Produto Produto { get; set; } = null!;
}
