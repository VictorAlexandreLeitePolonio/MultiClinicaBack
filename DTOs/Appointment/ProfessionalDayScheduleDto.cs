namespace MultiClinica.API.DTOs.Appointment;

public record AppointmentProfessionalDto(int Id, string Name);
public record DayAppointmentDto(int Id, string PatientName, DateTimeOffset Start, DateTimeOffset End);
public record DaySlotDto(DateTimeOffset Start, DateTimeOffset End, bool Available);
public record ProfessionalDayScheduleDto(DateOnly Date, string TimeZoneId, int DurationMinutes,
    IReadOnlyList<DayAppointmentDto> Appointments, IReadOnlyList<DaySlotDto> Slots);
