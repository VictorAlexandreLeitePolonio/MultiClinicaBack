namespace MultiClinica.API.Services;

internal static class PatientBirthDate
{
    public static DateOnly Today(TimeProvider clock, string? timeZoneId)
    {
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? "UTC"); }
        catch (TimeZoneNotFoundException) { timeZone = TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { timeZone = TimeZoneInfo.Utc; }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone).DateTime);
    }
}
