using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

/// <summary>
/// Clínica fora do marketplace (IsPublic=false) que aceita solicitações continua
/// agendável para pacientes já vinculados — e invisível para os demais.
/// </summary>
public class PrivateClinicAvailabilityTests
{
    private const string Password = "secret123";
    private sealed record SlotDto(DateTimeOffset Start, DateTimeOffset End, int Capacity);
    private sealed record AvailabilityDto(DateOnly Date, int DurationMinutes, string TimeZoneId, List<SlotDto> Slots);

    private sealed record Seed(int ClinicId, string LinkedEmail, string UnlinkedEmail, DateOnly Date);

    private static async Task<Seed> SeedPrivateClinicAsync(MultiClinicaFactory app, string suffix)
    {
        var scheduleDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3));
        var linkedEmail = $"private-linked-{suffix}@test.local";
        var unlinkedEmail = $"private-unlinked-{suffix}@test.local";
        var clinicId = 0;
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = "Clínica Privada",
                NomeResponsavel = "Responsável",
                IsPublic = false,
                AcceptsAppointmentRequests = true,
                TimeZoneId = "UTC",
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            clinicId = clinic.Id;

            var professional = new User
            {
                ClinicaId = clinic.Id,
                Name = "Profissional",
                Email = $"private-prof-{suffix}@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Profissional,
            };
            db.Users.Add(professional);
            db.ClinicBusinessHours.Add(new ClinicBusinessHour
            {
                ClinicaId = clinic.Id,
                DayOfWeek = scheduleDate.DayOfWeek,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(12, 0),
            });
            await db.SaveChangesAsync();
            db.ProfessionalAvailabilities.Add(new ProfessionalAvailability
            {
                ClinicaId = clinic.Id,
                UserId = professional.Id,
                DayOfWeek = scheduleDate.DayOfWeek,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(12, 0),
            });

            var linkedAccount = new PatientAccount
            {
                Name = "Paciente Vinculado",
                Email = linkedEmail,
                CPF = $"private-linked-{suffix}",
                Status = PatientAccountStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                ActivatedAt = DateTime.UtcNow,
            };
            db.PatientAccounts.AddRange(linkedAccount, new PatientAccount
            {
                Name = "Paciente Sem Vínculo",
                Email = unlinkedEmail,
                CPF = $"private-unlinked-{suffix}",
                Status = PatientAccountStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                ActivatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            db.Patients.Add(new Patient
            {
                ClinicaId = clinic.Id,
                PatientAccountId = linkedAccount.Id,
                Name = linkedAccount.Name,
            });
            await db.SaveChangesAsync();
        });
        return new Seed(clinicId, linkedEmail, unlinkedEmail, scheduleDate);
    }

    private static async Task<HttpClient> LoginPatientAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/patient-auth/login", new { email, password = Password }))
            .EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Linked_patient_sees_availability_and_creates_request_on_private_clinic()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedPrivateClinicAsync(app, "linked");
        using var patient = await LoginPatientAsync(app, seed.LinkedEmail);

        var availability = await patient.GetAsync(
            $"/api/patient/marketplace/clinics/{seed.ClinicId}/availability?date={seed.Date:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, availability.StatusCode);
        var dto = await availability.Content.ReadFromJsonAsync<AvailabilityDto>();
        Assert.NotEmpty(dto!.Slots);

        var created = await patient.PostAsJsonAsync("/api/patient/appointment-requests", new
        {
            clinicId = seed.ClinicId,
            requestedDate = dto.Slots[0].Start,
            reason = "Avaliação",
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
    }

    [Fact]
    public async Task Unlinked_patient_gets_not_found_on_private_clinic()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedPrivateClinicAsync(app, "unlinked");
        using var patient = await LoginPatientAsync(app, seed.UnlinkedEmail);

        var availability = await patient.GetAsync(
            $"/api/patient/marketplace/clinics/{seed.ClinicId}/availability?date={seed.Date:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.NotFound, availability.StatusCode);

        var created = await patient.PostAsJsonAsync("/api/patient/appointment-requests", new
        {
            clinicId = seed.ClinicId,
            requestedDate = seed.Date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
            reason = "Avaliação",
        });
        Assert.Equal(HttpStatusCode.NotFound, created.StatusCode);
    }
}
