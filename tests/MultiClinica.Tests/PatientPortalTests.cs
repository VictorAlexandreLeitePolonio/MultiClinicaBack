using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PatientPortalTests
{
    private const string Password = "senha-super-forte";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record MeDto(int Id, string? Name, string? Email, string? Cpf, string? Phone, PatientAccountStatus Status);
    private sealed record ApptDto(int AppointmentId, int ClinicId, string? ClinicName, string? ProfessionalName,
        DateTime AppointmentDate, AppointmentStatus Status);
    private sealed record ClinicDto(int Id, string? DisplayName, string? City, string? State,
        IReadOnlyList<string> Categories, int LikeCount, bool LikedByMe, bool AcceptsAppointmentRequests);

    // ── Seeding ──────────────────────────────────────────────────────────────

    private static async Task<int> SeedAccountAsync(AppDbContext db, string email)
    {
        var account = new PatientAccount
        {
            Name = "Paciente", Email = email, CPF = "12345678900",
            Status = PatientAccountStatus.Active,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            ActivatedAt = DateTime.UtcNow
        };
        db.PatientAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<(int clinicId, int professionalId, int patientId)> SeedClinicWithPatientAsync(
        AppDbContext db, string clinicName, int accountId)
    {
        var clinic = new Clinica { Nome = clinicName, NomeFantasia = clinicName, NomeResponsavel = "Victor", Cidade = "SP", Estado = "SP" };
        db.Clinicas.Add(clinic);
        await db.SaveChangesAsync();

        var professional = new User
        {
            ClinicaId = clinic.Id, Name = "Dr. " + clinicName, Email = $"prof-{clinic.Id}@test.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Profissional
        };
        db.Users.Add(professional);
        var patient = new Patient { ClinicaId = clinic.Id, PatientAccountId = accountId, Name = "Paciente" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        return (clinic.Id, professional.Id, patient.Id);
    }

    private static void SeedAppointment(AppDbContext db, int clinicId, int professionalId, int patientId,
        DateTime date, AppointmentStatus status)
        => db.Appointments.Add(new Appointment
        {
            ClinicaId = clinicId, UserId = professionalId, PatientId = patientId,
            AppointmentDate = date, Status = status
        });

    private static async Task<HttpClient> LoginPatientAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/patient-auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    // ── me ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_returns_only_authenticated_account()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            await SeedAccountAsync(db, "a@example.com");
            await SeedAccountAsync(db, "b@example.com");
        });

        using var client = await LoginPatientAsync(app, "a@example.com");
        var me = await client.GetFromJsonAsync<MeDto>("/api/patient/me", Json);

        Assert.NotNull(me);
        Assert.Equal("a@example.com", me!.Email);
    }

    [Fact]
    public async Task Update_me_changes_name_and_phone_only()
    {
        await using var app = new MultiClinicaFactory();
        int accountId = 0;
        await app.SeedAsync(async db => accountId = await SeedAccountAsync(db, "upd@example.com"));

        using var client = await LoginPatientAsync(app, "upd@example.com");
        // envia cpf/email extras — devem ser ignorados
        var response = await client.PatchAsJsonAsync("/api/patient/me",
            new { name = "Novo Nome", phone = "(11) 98888-7777", cpf = "99999999999", email = "hacker@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var account = await db.PatientAccounts.SingleAsync(a => a.Id == accountId);
            Assert.Equal("Novo Nome", account.Name);
            Assert.Equal("11988887777", account.Phone);
            Assert.Equal("12345678900", account.CPF);        // CPF read-only
            Assert.Equal("upd@example.com", account.Email);  // e-mail read-only
        });
    }

    // ── consultas ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Upcoming_and_history_are_filtered_and_ordered()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var accountId = await SeedAccountAsync(db, "appt@example.com");
            var (clinicId, profId, patientId) = await SeedClinicWithPatientAsync(db, "Clinica A", accountId);

            SeedAppointment(db, clinicId, profId, patientId, DateTime.UtcNow.AddDays(5), AppointmentStatus.Scheduled);   // upcoming
            SeedAppointment(db, clinicId, profId, patientId, DateTime.UtcNow.AddDays(2), AppointmentStatus.Scheduled);   // upcoming (mais cedo)
            SeedAppointment(db, clinicId, profId, patientId, DateTime.UtcNow.AddDays(-3), AppointmentStatus.Completed);  // history
            SeedAppointment(db, clinicId, profId, patientId, DateTime.UtcNow.AddDays(-1), AppointmentStatus.Cancelled);  // history
            await db.SaveChangesAsync();
        });

        using var client = await LoginPatientAsync(app, "appt@example.com");
        var upcoming = await client.GetFromJsonAsync<List<ApptDto>>("/api/patient/appointments/upcoming", Json);
        var history = await client.GetFromJsonAsync<List<ApptDto>>("/api/patient/appointments/history", Json);

        Assert.Equal(2, upcoming!.Count);
        Assert.True(upcoming[0].AppointmentDate < upcoming[1].AppointmentDate); // crescente
        Assert.Equal(2, history!.Count);
        Assert.True(history[0].AppointmentDate > history[1].AppointmentDate);   // decrescente
    }

    [Fact]
    public async Task Multi_clinic_appointments_appear_in_same_portal()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var accountId = await SeedAccountAsync(db, "multi@example.com");
            var a = await SeedClinicWithPatientAsync(db, "Clinica A", accountId);
            var b = await SeedClinicWithPatientAsync(db, "Clinica B", accountId);
            SeedAppointment(db, a.clinicId, a.professionalId, a.patientId, DateTime.UtcNow.AddDays(1), AppointmentStatus.Scheduled);
            SeedAppointment(db, b.clinicId, b.professionalId, b.patientId, DateTime.UtcNow.AddDays(2), AppointmentStatus.Scheduled);
            await db.SaveChangesAsync();
        });

        using var client = await LoginPatientAsync(app, "multi@example.com");
        var upcoming = await client.GetFromJsonAsync<List<ApptDto>>("/api/patient/appointments/upcoming", Json);

        Assert.Equal(2, upcoming!.Count);
        Assert.Equal(2, upcoming.Select(u => u.ClinicId).Distinct().Count());
    }

    [Fact]
    public async Task Patient_cannot_read_other_accounts_appointments()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var mine = await SeedAccountAsync(db, "mine@example.com");
            var other = await SeedAccountAsync(db, "other@example.com");
            var m = await SeedClinicWithPatientAsync(db, "Clinica Mine", mine);
            var o = await SeedClinicWithPatientAsync(db, "Clinica Other", other);
            SeedAppointment(db, m.clinicId, m.professionalId, m.patientId, DateTime.UtcNow.AddDays(1), AppointmentStatus.Scheduled);
            SeedAppointment(db, o.clinicId, o.professionalId, o.patientId, DateTime.UtcNow.AddDays(1), AppointmentStatus.Scheduled);
            await db.SaveChangesAsync();
        });

        using var client = await LoginPatientAsync(app, "mine@example.com");
        var upcoming = await client.GetFromJsonAsync<List<ApptDto>>("/api/patient/appointments/upcoming", Json);

        Assert.Single(upcoming!);
        Assert.Equal("Clinica Mine", upcoming![0].ClinicName);
    }

    // ── clínicas ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clinics_returns_linked_clinics_with_public_summary()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var accountId = await SeedAccountAsync(db, "clin@example.com");
            await SeedClinicWithPatientAsync(db, "Clinica A", accountId);
            await SeedClinicWithPatientAsync(db, "Clinica B", accountId);

            // Só a Clinica A aceita solicitações online.
            var clinicA = await db.Clinicas.FirstAsync(c => c.Nome == "Clinica A");
            clinicA.AcceptsAppointmentRequests = true;
            await db.SaveChangesAsync();
        });

        using var client = await LoginPatientAsync(app, "clin@example.com");
        var clinics = await client.GetFromJsonAsync<List<ClinicDto>>("/api/patient/clinics", Json);

        Assert.Equal(2, clinics!.Count);
        Assert.All(clinics, c => Assert.Empty(c.Categories));  // BACK-5/6 ainda não disponível
        Assert.All(clinics, c => Assert.Equal(0, c.LikeCount));
        // O flag acceptsAppointmentRequests é refletido por clínica (gate do CTA).
        Assert.True(clinics.Single(c => c.DisplayName == "Clinica A").AcceptsAppointmentRequests);
        Assert.False(clinics.Single(c => c.DisplayName == "Clinica B").AcceptsAppointmentRequests);
    }

    // ── privacidade ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Appointment_payload_does_not_leak_clinical_or_financial_data()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var accountId = await SeedAccountAsync(db, "priv@example.com");
            var (clinicId, profId, patientId) = await SeedClinicWithPatientAsync(db, "Clinica A", accountId);
            SeedAppointment(db, clinicId, profId, patientId, DateTime.UtcNow.AddDays(1), AppointmentStatus.Scheduled);
            await db.SaveChangesAsync();
        });

        using var client = await LoginPatientAsync(app, "priv@example.com");
        var raw = await (await client.GetAsync("/api/patient/appointments/upcoming")).Content.ReadAsStringAsync();

        foreach (var forbidden in new[] { "amount", "prontuario", "medicalRecord", "evolution", "anamnese", "payment", "cpf" })
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── isolamento de esquema ────────────────────────────────────────────────

    [Fact]
    public async Task Clinic_token_cannot_access_patient_portal()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id, Name = "Admin", Email = "admin@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Administrador
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@test.local", password = "secret123" });
        login.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/patient/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
