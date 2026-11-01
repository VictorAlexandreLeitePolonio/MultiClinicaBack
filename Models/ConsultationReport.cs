namespace MultiClinica.API.Models;

public enum ConsultationStatus { Recording, Processing, Ready, Interrupted, Failed }

// Consulta gravada (1:1 com Appointment): transcrição + relatório SOAP. O áudio fica em disco.
public class ConsultationReport : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int ProfessionalId { get; set; }
    public int RecordedByUserId { get; set; }
    public DateTime ConsentConfirmedAt { get; set; }

    public ConsultationStatus Status { get; set; } = ConsultationStatus.Recording;
    public string? FailureReason { get; set; }
    public int ProcessingAttempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? LastChunkAt { get; set; }
    public int ChunkCount { get; set; }
    public int? DurationSeconds { get; set; }

    public string? Transcript { get; set; }
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }
    public int? SuggestedReturnDays { get; set; }
    // Transcrição pronta, relatório ainda não gerado (IA indisponível).
    public bool ReportPending { get; set; }
    public int MinutesCharged { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }

    public Appointment Appointment { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public User Professional { get; set; } = null!;
}
