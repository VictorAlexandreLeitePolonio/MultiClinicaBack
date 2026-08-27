using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class AvailabilityTests
{
    private const string Password = "secret123";
    private sealed record SettingsDto(int SlotDurationMinutes, string TimeZoneId);
    private sealed record SlotDto(DateTimeOffset Start, DateTimeOffset End, int Capacity);
    private sealed record AvailabilityDto(DateOnly Date, int DurationMinutes, string TimeZoneId, List<SlotDto> Slots);
    private sealed record ErrorDto(string Code, string Message);

    [Fact]
    public async Task Clinic_reads_default_availability_settings()
    {
        await using var app = new MultiClinicaFactory();
        var email = await SeedClinicAdminAsync(app, "settings");
        using var client = await LoginClinicAsync(app, email);

        var response = await client.GetAsync("/api/clinic/availability/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await response.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Equal(60, settings!.SlotDurationMinutes);
        Assert.Equal("America/Sao_Paulo", settings.TimeZoneId);
    }

    [Fact]
    public async Task Clinic_updates_valid_settings_and_rejects_invalid_values()
    {
        await using var app = new MultiClinicaFactory();
        var email = await SeedClinicAdminAsync(app, "settings-update");
        using var client = await LoginClinicAsync(app, email);

        var valid = await client.PutAsJsonAsync("/api/clinic/availability/settings", new
        {
            slotDurationMinutes = 45,
            timeZoneId = "UTC",
        });
        var invalidDuration = await client.PutAsJsonAsync("/api/clinic/availability/settings", new
        {
            slotDurationMinutes = 17,
            timeZoneId = "UTC",
        });
        var invalidTimeZone = await client.PutAsJsonAsync("/api/clinic/availability/settings", new
        {
            slotDurationMinutes = 30,
            timeZoneId = "Mars/Olympus",
        });

        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.Equal(45, (await valid.Content.ReadFromJsonAsync<SettingsDto>())!.SlotDurationMinutes);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDuration.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTimeZone.StatusCode);
    }

    [Fact]
    public async Task Professional_ranges_are_replaced_atomically_and_cannot_overlap()
    {
        await using var app = new MultiClinicaFactory();
        var seeded = await SeedAvailabilityAsync(app, "ranges", addSchedule: false);
        using var client = await LoginClinicAsync(app, seeded.AdminEmail);
        var route = $"/api/clinic/availability/professionals/{seeded.ProfessionalId}";

        var valid = await client.PutAsJsonAsync(route, new[]
        {
            new { dayOfWeek = DayOfWeek.Monday, startTime = "08:00:00", endTime = "12:00:00" },
            new { dayOfWeek = DayOfWeek.Monday, startTime = "13:00:00", endTime = "17:00:00" },
        });
        var overlap = await client.PutAsJsonAsync(route, new[]
        {
            new { dayOfWeek = DayOfWeek.Monday, startTime = "08:00:00", endTime = "12:00:00" },
            new { dayOfWeek = DayOfWeek.Monday, startTime = "11:00:00", endTime = "13:00:00" },
        });

        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        await app.SeedAsync(async db => Assert.Equal(2, await db.ProfessionalAvailabilities.CountAsync()));
    }

    [Fact]
    public async Task Patient_availability_respects_professional_capacity_and_pending_requests()
    {
        await using var app = new MultiClinicaFactory();
        var seeded = await SeedAvailabilityAsync(app, "slots", addSchedule: true);
        using var patient = await LoginPatientAsync(app, seeded.PatientEmail);

        var response = await patient.GetAsync(
            $"/api/patient/marketplace/clinics/{seeded.ClinicId}/availability?date={seeded.Date:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availability = await response.Content.ReadFromJsonAsync<AvailabilityDto>();
        Assert.Equal(60, availability!.DurationMinutes);
        Assert.Equal("UTC", availability.TimeZoneId);
        var tenOClock = Assert.Single(availability.Slots, s => s.Start.Hour == 10);
        Assert.Equal(1, tenOClock.Capacity);
        Assert.Equal(1, Assert.Single(availability.Slots, s => s.Start.Hour == 9).Capacity);
        Assert.Equal(2, Assert.Single(availability.Slots, s => s.Start.Hour == 11).Capacity);
        Assert.Equal(3, availability.Slots.Count);
        Assert.Equal(TimeSpan.Zero, tenOClock.Start.Offset);
    }

    [Fact]
    public async Task Appointment_request_must_match_an_available_slot_exactly()
    {
        await using var app = new MultiClinicaFactory();
        var seeded = await SeedAvailabilityAsync(app, "exact-slot", addSchedule: true);
        using var patient = await LoginPatientAsync(app, seeded.PatientEmail);

        var response = await patient.PostAsJsonAsync("/api/patient/appointment-requests", new
        {
            clinicId = seeded.ClinicId,
            requestedDate = new DateTimeOffset(
                seeded.Date.ToDateTime(new TimeOnly(10, 30), DateTimeKind.Utc)),
            reason = "Avaliação",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.Equal("SlotUnavailable", error!.Code);
        await app.SeedAsync(async db => Assert.Equal(2, await db.AppointmentRequests.CountAsync()));
    }

    [Fact]
    public async Task Availability_never_returns_past_slots()
    {
        await using var app = new MultiClinicaFactory();
        var seeded = await SeedAvailabilityAsync(
            app, "past", addSchedule: true, date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        using var patient = await LoginPatientAsync(app, seeded.PatientEmail);

        var availability = await patient.GetFromJsonAsync<AvailabilityDto>(
            $"/api/patient/marketplace/clinics/{seeded.ClinicId}/availability?date={seeded.Date:yyyy-MM-dd}");

        Assert.Empty(availability!.Slots);
    }

    private static async Task<string> SeedClinicAdminAsync(MultiClinicaFactory app, string suffix)
    {
        var email = $"availability-{suffix}@test.local";
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica", NomeResponsavel = "Responsável" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Administrador,
            });
            await db.SaveChangesAsync();
        });
        return email;
    }

    private static async Task<HttpClient> LoginClinicAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password }))
            .EnsureSuccessStatusCode();
        return client;
    }

    private sealed record AvailabilitySeed(
        int ClinicId, int ProfessionalId, string AdminEmail, string PatientEmail, DateOnly Date);

    private static async Task<AvailabilitySeed> SeedAvailabilityAsync(
        MultiClinicaFactory app, string suffix, bool addSchedule, DateOnly? date = null)
    {
        var scheduleDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3));
        var adminEmail = $"availability-admin-{suffix}@test.local";
        var patientEmail = $"availability-patient-{suffix}@test.local";
        var clinicId = 0;
        var professionalId = 0;
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = "Clínica",
                NomeResponsavel = "Responsável",
                IsPublic = true,
                AcceptsAppointmentRequests = true,
                TimeZoneId = "UTC",
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            clinicId = clinic.Id;
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Administrador,
            });
            var professional = new User
            {
                ClinicaId = clinic.Id,
                Name = "Profissional",
                Email = $"availability-prof-{suffix}@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Profissional,
            };
            db.Users.Add(professional);
            db.PatientAccounts.Add(new PatientAccount
            {
                Name = "Paciente",
                Email = patientEmail,
                CPF = $"availability-{suffix}",
                Status = PatientAccountStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                ActivatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            professionalId = professional.Id;
            if (!addSchedule)
                return;

            db.ClinicBusinessHours.Add(new ClinicBusinessHour
            {
                ClinicaId = clinic.Id,
                DayOfWeek = scheduleDate.DayOfWeek,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(12, 0),
            });
            foreach (var userId in new[] { professional.Id, db.Users.Single(u => u.Email == adminEmail).Id })
            {
                db.ProfessionalAvailabilities.Add(new ProfessionalAvailability
                {
                    ClinicaId = clinic.Id,
                    UserId = userId,
                    DayOfWeek = scheduleDate.DayOfWeek,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(12, 0),
                });
            }
            var patientAccount = db.PatientAccounts.Single(a => a.Email == patientEmail);
            var linkedPatient = new Patient
            {
                ClinicaId = clinic.Id,
                PatientAccountId = patientAccount.Id,
                Name = patientAccount.Name,
            };
            db.Patients.Add(linkedPatient);
            await db.SaveChangesAsync();
            db.Appointments.AddRange(
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = professional.Id,
                    PatientId = linkedPatient.Id,
                    AppointmentDate = scheduleDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
                    DurationMinutes = 60,
                    Status = AppointmentStatus.Scheduled,
                },
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = professional.Id,
                    PatientId = linkedPatient.Id,
                    AppointmentDate = scheduleDate.ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc),
                    DurationMinutes = 60,
                    Status = AppointmentStatus.Completed,
                });
            db.AppointmentRequests.AddRange(new AppointmentRequest
            {
                PatientAccountId = patientAccount.Id,
                ClinicaId = clinic.Id,
                RequestedDate = scheduleDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc),
                DurationMinutes = 60,
                Status = AppointmentRequestStatus.Pending,
            }, new AppointmentRequest
            {
                PatientAccountId = patientAccount.Id,
                ClinicaId = clinic.Id,
                RequestedDate = scheduleDate.ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc),
                DurationMinutes = 60,
                Status = AppointmentRequestStatus.Rejected,
            });
            await db.SaveChangesAsync();
        });
        return new AvailabilitySeed(clinicId, professionalId, adminEmail, patientEmail, scheduleDate);
    }

    private static async Task<HttpClient> LoginPatientAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/patient-auth/login", new { email, password = Password }))
            .EnsureSuccessStatusCode();
        return client;
    }
}
