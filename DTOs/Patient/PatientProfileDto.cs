namespace ProjetoLP.API.DTOs.Patient;

using ProjetoLP.API.Models;

// Retorno da rota GET /api/patients/{id}/profile
// Agrega dados do paciente + histórico de consultas, prontuários e pagamentos em uma única chamada.
public class PatientProfileDto
{
    // ── Dados cadastrais ────────────────────────────────────────────────────
    public int    Id       { get; set; }
    public string? Name    { get; set; }
    public string? Email   { get; set; }
    public string? CPF     { get; set; }
    public string? Rg      { get; set; }
    public string? Phone   { get; set; }
    public string? Rua     { get; set; }
    public string? Numero  { get; set; }
    public string? Bairro  { get; set; }
    public string? Cidade  { get; set; }
    public string? Estado  { get; set; }
    public string? Cep     { get; set; }
    public bool   IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // ── Históricos ──────────────────────────────────────────────────────────
    public List<AppointmentSummary>   Appointments   { get; set; } = [];
    public List<MedicalRecordSummary> MedicalRecords { get; set; } = [];
    public List<PaymentSummary>       Payments       { get; set; } = [];
}

public class AppointmentSummary
{
    public int               Id              { get; set; }
    public DateTime          AppointmentDate { get; set; }
    public AppointmentStatus Status          { get; set; }
    public string            UserName        { get; set; } = string.Empty;
    public DateTime          CreatedAt       { get; set; }
}

public class MedicalRecordSummary
{
    public int      Id        { get; set; }
    public string   Titulo    { get; set; } = string.Empty;
    public string   Sessao    { get; set; } = string.Empty;
    public string   Patologia { get; set; } = string.Empty;
    public string   UserName  { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class PaymentSummary
{
    public int           Id                  { get; set; }
    public string        ReferenceMonth      { get; set; } = string.Empty;
    public string        PlanName            { get; set; } = string.Empty;
    public decimal       Amount              { get; set; }
    public string        PaymentMethod       { get; set; } = string.Empty;
    public PaymentStatus Status              { get; set; }
    public DateTime?     PaymentDate         { get; set; }
    public DateTime?     PaidAt              { get; set; }
    public bool          PaymentReminderSent { get; set; }
    public DateTime      CreatedAt           { get; set; }
}
