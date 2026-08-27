namespace MultiClinica.API.DTOs.Availability;

public sealed class AvailabilitySettingsDto
{
    public int SlotDurationMinutes { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
}

public sealed class UpdateAvailabilitySettingsDto
{
    public int SlotDurationMinutes { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
}

public sealed class ProfessionalAvailabilityRangeDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class AvailabilitySlotDto
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int Capacity { get; set; }
}

public sealed class ClinicAvailabilityDto
{
    public DateOnly Date { get; set; }
    public int DurationMinutes { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public IReadOnlyList<AvailabilitySlotDto> Slots { get; set; } = [];
}
