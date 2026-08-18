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

    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    /// <summary>Se a clínica aceita solicitações de consulta do portal do paciente (BACK-4/BACK-5).</summary>
    public bool AcceptsAppointmentRequests { get; set; }

    // ── Presença pública (BACK-5) ────────────────────────────────────────────
    public string? PublicSlug { get; set; }
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Contador materializado de likes (BACK-6) — leitura rápida, sem COUNT(*).</summary>
    public int LikeCount { get; set; }

    public ICollection<ClinicCategory> Categories { get; set; } = [];
    public ICollection<ClinicBusinessHour> BusinessHours { get; set; } = [];
    public ICollection<ClinicMedia> Media { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Patient> Patients { get; set; } = [];
    public ICollection<ClinicCharge> Charges { get; set; } = [];
    public ICollection<CommercialHistoryEvent> CommercialHistory { get; set; } = [];
    public ICollection<EvolutionTemplate> EvolutionTemplates { get; set; } = [];
}
