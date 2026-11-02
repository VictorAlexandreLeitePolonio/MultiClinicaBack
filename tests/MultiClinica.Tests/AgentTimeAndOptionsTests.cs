using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class AgentTimeAndOptionsTests
{
    private static readonly TimeZoneInfo Sp = AgentTime.FindZone("America/Sao_Paulo");

    [Fact]
    public void Local_iso_converts_to_utc_using_clinic_zone()
        => Assert.Equal(new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
            AgentTime.ToUtc("2026-10-13T09:00", Sp));

    [Fact]
    public void Utc_formats_back_to_local_iso()
        => Assert.Equal("2026-10-13T09:00",
            AgentTime.ToLocalIso(new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc), Sp));

    [Theory]
    [InlineData("13/10/2026 09:00")]
    [InlineData("2026-10-13")]
    [InlineData("terça 9h")]
    public void Invalid_formats_throw_readable_error(string value)
    {
        var ex = Assert.Throws<FormatException>(() => AgentTime.ToUtc(value, Sp));
        Assert.Contains("yyyy-MM-ddTHH:mm", ex.Message);
    }

    [Fact]
    public void Day_range_covers_whole_local_days()
    {
        var (start, end) = AgentTime.DayRangeUtc(new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 13), Sp);
        Assert.Equal(new DateTime(2026, 10, 13, 3, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 14, 3, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void Profile_model_overrides_default_only_when_set()
    {
        var options = new AgentOptions { Model = "glm-5.2", OwnerModel = "kimi-k3" };
        Assert.Equal("kimi-k3", options.ModelFor(AgentProfile.Owner));
        Assert.Equal("glm-5.2", options.ModelFor(AgentProfile.Patient));
    }
}
