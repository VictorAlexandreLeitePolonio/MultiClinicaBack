namespace MultiClinica.API.DTOs.Consultation;

public sealed record StartConsultationDto(bool ConsentConfirmed);
public sealed record StartConsultationResponse(int ReportId);

public sealed record ConsultationChargeDto(int PlanId, string PaymentMethod, bool SendOnline);
public sealed record FinishConsultationDto(ConsultationChargeDto? Charge);
public sealed record FinishConsultationResponse(string Status, int? PaymentId, string? ChargeError);

public sealed record ConsultationReportDto(
    int Id, int AppointmentId, int PatientId, int ProfessionalId, string Status, string? FailureReason,
    DateTime StartedAt, DateTime? EndedAt, int ChunkCount, int? DurationSeconds, string? Transcript,
    string? Subjective, string? Objective, string? Assessment, string? Plan, int? SuggestedReturnDays,
    bool ReportPending, int MinutesCharged, DateTime? ReviewedAt, int? ReviewedByUserId);

public sealed record UpdateConsultationReportDto(string? Subjective, string? Objective, string? Assessment, string? Plan);

public sealed record ChargeSuggestionDto(int? PlanId, string? PlanName, string? PlanType, bool AlreadyChargedThisMonth);

public sealed record CreateEvolutionFromConsultationDto(int TreatmentId);
public sealed record CreateEvolutionFromConsultationResponse(int EvolutionId);

public sealed record ConsultationReportSummaryDto(
    int Id, int AppointmentId, DateTime AppointmentDate, string ProfessionalName, string Status, bool Reviewed);
