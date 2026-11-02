using System.Net.Http.Json;
using System.Text.Json;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

// Filtros de data da agenda valem para o dia no fuso da clínica, não no dia UTC.
public class AppointmentDateFilterTests
{
    [Theory]
    [InlineData("date=2026-11-10")]
    [InlineData("dateFrom=2026-11-10&dateTo=2026-11-10")]
    public async Task Late_evening_appointment_belongs_to_the_clinic_local_day(string filter)
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        // 22:30 em São Paulo = 01:30 UTC do dia seguinte.
        var lateId = 0;
        await app.SeedAsync(async db =>
        {
            var late = new Appointment
            {
                ClinicaId = seed.ClinicaId, UserId = seed.ProfessionalId, PatientId = seed.PatientId,
                AppointmentDate = new DateTime(2026, 11, 11, 1, 30, 0, DateTimeKind.Utc),
            };
            db.Appointments.Add(late);
            await db.SaveChangesAsync();
            lateId = late.Id;
        });
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.AdminEmail);

        var sameDay = await client.GetFromJsonAsync<JsonElement>($"/api/appointments?{filter}");
        var nextDay = await client.GetFromJsonAsync<JsonElement>(
            $"/api/appointments?{filter.Replace("2026-11-10", "2026-11-11")}");

        Assert.Contains(lateId, sameDay.GetProperty("data").EnumerateArray().Select(a => a.GetProperty("id").GetInt32()));
        Assert.DoesNotContain(lateId, nextDay.GetProperty("data").EnumerateArray().Select(a => a.GetProperty("id").GetInt32()));
    }
}
