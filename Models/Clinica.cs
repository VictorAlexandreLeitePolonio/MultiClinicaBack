namespace MultiClinica.API.Models;

public class Clinica : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string NomeResponsavel { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Rua { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;
    public decimal ValorMensalidade { get; set; }
    public int DiaVencimento { get; set; } = 10;
    public bool CobrancaAtiva { get; set; }
    public DateOnly? DataInicioCobranca { get; set; }
    public bool IsBlockedByBilling { get; set; }
    public string? BillingBlockReason { get; set; }
    public DateTime? BillingBlockedAt { get; set; }

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Patient> Patients { get; set; } = [];
    public ICollection<ClinicCharge> Charges { get; set; } = [];
    public ICollection<CommercialHistoryEvent> CommercialHistory { get; set; } = [];
    public ICollection<EvolutionTemplate> EvolutionTemplates { get; set; } = [];
}
