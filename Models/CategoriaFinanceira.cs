namespace MultiClinica.API.Models;

public enum TipoCategoriaFinanceira
{
    Receita,
    Despesa
}

public class CategoriaFinanceira : AuditableEntity
{
    public int ClinicaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TipoCategoriaFinanceira Tipo { get; set; }

    public Clinica Clinica { get; set; } = null!;
}
