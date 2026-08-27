namespace MultiClinica.API.Models;

public enum AppointmentRequestStatus
{
    Pending,
    Accepted,
    Rejected,
    Cancelled
}

/// <summary>Quem originou o cancelamento — definido pelo backend, nunca pelo cliente.</summary>
public enum CancellationOrigin
{
    Patient,
    Clinic
}

/// <summary>
/// Solicitação de consulta feita pelo paciente. Domínio separado de
/// <see cref="Appointment"/>: somente o aceite cria uma consulta real.
/// </summary>
public class AppointmentRequest : AuditableEntity
{
    public int PatientAccountId { get; set; }
    public PatientAccount PatientAccount { get; set; } = null!;

    public int ClinicaId { get; set; }
    public Clinica Clinica { get; set; } = null!;

    public DateTime RequestedDate { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public string? Reason { get; set; }

    public AppointmentRequestStatus Status { get; set; } = AppointmentRequestStatus.Pending;

    /// <summary>Motivo da recusa/cancelamento pela clínica ou paciente.</summary>
    public string? ResponseReason { get; set; }
    public CancellationOrigin? CancelledBy { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>Consulta criada no aceite (opcional até ser aceita).</summary>
    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
}
