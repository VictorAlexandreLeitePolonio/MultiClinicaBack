using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.AppointmentRequest;

// ── Entrada ──────────────────────────────────────────────────────────────────

public class CreateAppointmentRequestDto
{
    public int ClinicId { get; set; }
    public DateTimeOffset RequestedDate { get; set; }
    public string? Reason { get; set; }
}

public class AcceptAppointmentRequestDto
{
    public int ProfessionalId { get; set; }
}

public class ReasonDto
{
    public string? Reason { get; set; }
}

// ── Saída ────────────────────────────────────────────────────────────────────

/// <summary>Solicitação como vista pelo paciente ou pela clínica.</summary>
public class AppointmentRequestDto
{
    public int Id { get; set; }
    public int PatientAccountId { get; set; }
    public int ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public string? PatientName { get; set; }
    public DateTime RequestedDate { get; set; }
    public int DurationMinutes { get; set; }
    public string? Reason { get; set; }
    public AppointmentRequestStatus Status { get; set; }
    public string? ResponseReason { get; set; }
    public CancellationOrigin? CancelledBy { get; set; }
    public DateTime? RespondedAt { get; set; }
    public int? AppointmentId { get; set; }
    public DateTime CreatedAt { get; set; }
}
