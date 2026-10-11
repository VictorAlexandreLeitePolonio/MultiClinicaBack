using System.Globalization;

namespace MultiClinica.API.Services.Agent;

// Única fronteira de fuso entre o LLM (horário local da clínica) e o banco (UTC).
public static class AgentTime
{
    private const string Format = "yyyy-MM-ddTHH:mm";

    public static TimeZoneInfo FindZone(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);

    public static DateTime ToUtc(string localIso, TimeZoneInfo tz)
    {
        if (!DateTime.TryParseExact(localIso, [Format, "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
            throw new FormatException($"Data/hora inválida '{localIso}'. Use o formato {Format}.");
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);
    }

    public static string ToLocalIso(DateTime utc, TimeZoneInfo tz)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz)
            .ToString(Format, CultureInfo.InvariantCulture);

    public static DateOnly ParseDate(string value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new FormatException($"Data inválida '{value}'. Use o formato yyyy-MM-dd.");

    public static (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly from, DateOnly to, TimeZoneInfo tz)
        => (TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), tz),
            TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), tz));
}
