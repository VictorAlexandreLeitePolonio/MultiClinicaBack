namespace MultiClinica.API.Models;

public class Fornecedor : AuditableEntity
{
    public int ClinicaId { get; set; }
    public string Nome { get; set; } = string.Empty;

    public Clinica Clinica { get; set; } = null!;
}
