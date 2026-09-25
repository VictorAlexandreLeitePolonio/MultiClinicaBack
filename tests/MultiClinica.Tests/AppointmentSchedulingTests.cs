using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using Npgsql;
using Xunit;

namespace MultiClinica.Tests;

public class AppointmentSchedulingTests
{
    [Fact]
    public async Task Concurrent_bookings_on_postgres_allow_only_one()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("MULTICLINICA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnectionString)) return;

        var databaseName = $"multiclinica_schedule_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString).Options;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.Clinicas.Add(new Clinica { Id = 1, Nome = "Teste" });
                db.Users.Add(new User { Id = 1, ClinicaId = 1, Name = "Profissional", Email = "concurrent@test.local" });
                db.Patients.Add(new Patient { Id = 1, ClinicaId = 1, Name = "Paciente" });
                await db.SaveChangesAsync();
            }

            var date = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(3).AddHours(13), DateTimeKind.Utc);
            async Task<Result<int>> Book()
            {
                await using var db = new AppDbContext(options);
                var guard = new AppointmentBookingGuard(db);
                return await guard.RunAsync(1, 1, date, 60, null, async () =>
                {
                    db.Appointments.Add(new Appointment { ClinicaId = 1, UserId = 1, PatientId = 1,
                        AppointmentDate = date, DurationMinutes = 60 });
                    return await db.SaveChangesAsync();
                });
            }

            var results = await Task.WhenAll(Book(), Book());
            Assert.Single(results, r => r.IsSuccess);
            Assert.Single(results, r => r.ErrorCode == ErrorCodes.AppointmentConflict);
            await using var verify = new AppDbContext(options);
            Assert.Equal(1, await verify.Appointments.CountAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Admin_books_another_professional_and_overlap_is_rejected()
    {
        await using var app = new MultiClinicaFactory();
        var adminId = 0;
        var professionalId = 0;
        var receptionId = 0;
        var patientId = 0;
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica", Email = "schedule-clinic@test.local", TimeZoneId = "America/Sao_Paulo", AppointmentSlotDurationMinutes = 60 };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            var admin = new User { ClinicaId = clinic.Id, Name = "Admin", Email = "schedule-admin@test.local", PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Administrador };
            var professional = new User { ClinicaId = clinic.Id, Name = "Profissional", Email = "schedule-prof@test.local", Role = UserRole.Profissional };
            var reception = new User { ClinicaId = clinic.Id, Name = "Recepção", Email = "schedule-reception@test.local", PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Recepcao };
            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente" };
            db.AddRange(admin, professional, reception, patient);
            await db.SaveChangesAsync();
            adminId = admin.Id;
            professionalId = professional.Id;
            receptionId = reception.Id;
            patientId = patient.Id;
        });

        using var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { email = "schedule-admin@test.local", password = "secret123" })).EnsureSuccessStatusCode();
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4));
        var first = await client.PostAsJsonAsync("/api/appointments", new { professionalId, patientId, appointmentDate = $"{day:yyyy-MM-dd}T13:00:00Z" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var firstId = firstJson.RootElement.GetProperty("id").GetInt32();
        var overlap = await client.PostAsJsonAsync("/api/appointments", new { professionalId, patientId, appointmentDate = $"{day:yyyy-MM-dd}T13:30:00Z" });
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        var adjacent = await client.PostAsJsonAsync("/api/appointments", new { professionalId, patientId, appointmentDate = $"{day:yyyy-MM-dd}T14:00:00Z" });
        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
        using var adjacentJson = JsonDocument.Parse(await adjacent.Content.ReadAsStringAsync());
        var adjacentId = adjacentJson.RootElement.GetProperty("id").GetInt32();
        var movedIntoConflict = await client.PutAsJsonAsync($"/api/appointments/{adjacentId}", new { appointmentDate = $"{day:yyyy-MM-dd}T13:30:00Z", status = "Scheduled" });
        Assert.Equal(HttpStatusCode.Conflict, movedIntoConflict.StatusCode);
        var sameSlot = await client.PutAsJsonAsync($"/api/appointments/{firstId}", new { appointmentDate = $"{day:yyyy-MM-dd}T13:00:00Z", status = "Scheduled" });
        Assert.Equal(HttpStatusCode.OK, sameSlot.StatusCode);
        using var detail = JsonDocument.Parse(await client.GetStringAsync($"/api/appointments/{firstId}"));
        Assert.Equal("America/Sao_Paulo", detail.RootElement.GetProperty("timeZoneId").GetString());
        var professionals = await client.GetAsync("/api/appointments/professionals");
        Assert.Equal(HttpStatusCode.OK, professionals.StatusCode);
        using var list = JsonDocument.Parse(await professionals.Content.ReadAsStringAsync());
        Assert.Contains(list.RootElement.EnumerateArray(), item => item.GetProperty("id").GetInt32() == professionalId);
        Assert.DoesNotContain(list.RootElement.EnumerateArray(), item => item.GetProperty("id").GetInt32() == receptionId);
        var schedule = await client.GetAsync($"/api/appointments/day-schedule?professionalId={professionalId}&date={day:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        using var body = JsonDocument.Parse(await schedule.Content.ReadAsStringAsync());
        Assert.Equal("America/Sao_Paulo", body.RootElement.GetProperty("timeZoneId").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("appointments").GetArrayLength());
        Assert.Contains(body.RootElement.GetProperty("slots").EnumerateArray(), slot => !slot.GetProperty("available").GetBoolean());
        using var receptionClient = app.CreateClient();
        (await receptionClient.PostAsJsonAsync("/api/auth/login", new { email = "schedule-reception@test.local", password = "secret123" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await receptionClient.GetAsync("/api/appointments/professionals")).StatusCode);
        var invalid = await client.PostAsJsonAsync("/api/appointments", new { professionalId = receptionId, patientId, appointmentDate = $"{day:yyyy-MM-dd}T15:00:00Z" });
        Assert.NotEqual(HttpStatusCode.Created, invalid.StatusCode);
        await app.SeedAsync(async db =>
        {
            var appointments = await db.Appointments.OrderBy(a => a.AppointmentDate).ToListAsync();
            Assert.Equal(2, appointments.Count);
            Assert.All(appointments, a => { Assert.Equal(professionalId, a.UserId); Assert.Equal(adminId, a.CreatedByUserId); Assert.Equal(60, a.DurationMinutes); });
            Assert.Equal(13, appointments[0].AppointmentDate.Hour);
        });
        var legacyReceptionAppointmentId = 0;
        await app.SeedAsync(async db =>
        {
            var appointment = new Appointment { ClinicaId = db.Users.Single(u => u.Id == receptionId).ClinicaId,
                UserId = receptionId, PatientId = patientId,
                AppointmentDate = DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(16)), DateTimeKind.Utc) };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            legacyReceptionAppointmentId = appointment.Id;
        });
        var legacyMoved = await client.PutAsJsonAsync($"/api/appointments/{legacyReceptionAppointmentId}",
            new { appointmentDate = $"{day:yyyy-MM-dd}T17:00:00Z", status = "Scheduled" });
        Assert.NotEqual(HttpStatusCode.OK, legacyMoved.StatusCode);
    }
}
