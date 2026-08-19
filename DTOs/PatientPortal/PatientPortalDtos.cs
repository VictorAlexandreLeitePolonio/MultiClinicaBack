using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.PatientPortal;

/// <summary>Perfil do paciente no portal (contrato público, sem dados clínicos).</summary>
public class PatientMeDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? CPF { get; set; }
    public string? Phone { get; set; }
    public PatientAccountStatus Status { get; set; }
}

/// <summary>MVP: paciente só pode alterar nome e telefone (CPF/e-mail read-only).</summary>
public class UpdatePatientMeDto
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
}

/// <summary>Consulta exposta ao paciente — sem prontuário, evolução ou pagamentos.</summary>
public class PatientAppointmentDto
{
    public int AppointmentId { get; set; }
    public int ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public string? ClinicSlug { get; set; }
    public string? ProfessionalName { get; set; }
    public DateTime AppointmentDate { get; set; }
    public AppointmentStatus Status { get; set; }
}

/// <summary>Resumo público de uma clínica vinculada ("Minhas Clínicas").</summary>
public class PatientClinicDto
{
    public int Id { get; set; }
    public string? Slug { get; set; }
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverUrl { get; set; }
    public IEnumerable<string> Categories { get; set; } = [];
    public string? City { get; set; }
    public string? State { get; set; }
    public int LikeCount { get; set; }
    public bool LikedByMe { get; set; }
    /// <summary>Se a clínica aceita solicitações de consulta online (gate do CTA "Solicitar consulta").</summary>
    public bool AcceptsAppointmentRequests { get; set; }
}
