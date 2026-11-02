namespace MultiClinica.API.DTOs.Evolution;

// GET api/patients/{patientId}/measurements: uma série por ficha medida.
public sealed record PatientMeasurementSeriesDto(
    int TemplateId, string TemplateName, DateTime LastDate, int EvolutionCount,
    IReadOnlyList<TreatmentProgressChartDto> Fields);

// GET api/patients/{patientId}/timeline: Type = "appointment" | "evolution" | "anamnesis".
public sealed record PatientTimelineEventDto(
    string Type, int Id, DateTime Date, string Title, string? ProfessionalName, string? Status,
    string? ConsultationStatus, int? AppointmentId, int EvolutionCount, IReadOnlyList<string> TemplateNames);

// NextBefore: passe em ?before= para a próxima página; null quando acabou.
public sealed record PatientTimelineDto(IReadOnlyList<PatientTimelineEventDto> Events, DateTime? NextBefore);

// Linha do tempo da clínica (GET api/patient-records/timeline): mesmo evento + de qual paciente é.
public sealed record ClinicTimelineEventDto(int PatientId, string PatientName, PatientTimelineEventDto Event);
public sealed record ClinicTimelineDto(IReadOnlyList<ClinicTimelineEventDto> Events, DateTime? NextBefore);
