using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PatientAccountTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record CreatedResponse(
        int Id,
        int PatientId,
        int PatientAccountId,
        PatientAccountStatus PatientAccountStatus,
        PatientPortalLinkResult LinkResult,
        bool InvitationSent);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task SeedClinicAsync(AppDbContext db, string name, string adminEmail)
    {
        var clinic = new Clinica { Nome = name, NomeResponsavel = "Victor" };
        db.Clinicas.Add(clinic);
        await db.SaveChangesAsync();

        db.Users.Add(new User
        {
            ClinicaId = clinic.Id,
            Name = "Admin " + name,
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
            Role = UserRole.Administrador
        });
        await db.SaveChangesAsync();
    }

    private static async Task<HttpClient> LoginAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = "secret123"
        });
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static object NewPatient(string email, string cpf, string phone = "(11) 99999-9999", string name = "Paciente") =>
        new { name, email, cpf, phone };

    // ── 1. Criação de conta global nova ──────────────────────────────────────

    [Fact]
    public async Task Create_new_patient_creates_global_account_pending_activation()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        var response = await client.PostAsJsonAsync("/api/patients",
            NewPatient("Joao@Example.com", "123.456.789-00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>(Json);

        Assert.NotNull(body);
        Assert.True(body!.PatientAccountId > 0);
        Assert.Equal(PatientAccountStatus.PendingActivation, body.PatientAccountStatus);
        Assert.Equal(PatientPortalLinkResult.CreatedAccount, body.LinkResult);
        Assert.False(body.InvitationSent); // stub — envio real em BACK-2

        await app.SeedAsync(async db =>
        {
            var account = await db.PatientAccounts.SingleAsync();
            Assert.Equal("joao@example.com", account.Email); // normalizado (lowercase)
            Assert.Null(account.PasswordHash);               // clínica não define senha
        });
    }

    // ── 2 & 3. Reutilização/vínculo em outra clínica ─────────────────────────

    [Fact]
    public async Task Same_email_in_second_clinic_links_existing_account()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            await SeedClinicAsync(db, "Clinica A", "admin-a@test.local");
            await SeedClinicAsync(db, "Clinica B", "admin-b@test.local");
        });

        using (var clientA = await LoginAsync(app, "admin-a@test.local"))
        {
            var r = await clientA.PostAsJsonAsync("/api/patients", NewPatient("maria@example.com", "111.222.333-44"));
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        using var clientB = await LoginAsync(app, "admin-b@test.local");
        var response = await clientB.PostAsJsonAsync("/api/patients", NewPatient("maria@example.com", "111.222.333-44"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>(Json);
        Assert.Equal(PatientPortalLinkResult.LinkedExistingAccount, body!.LinkResult);

        await app.SeedAsync(async db =>
        {
            var account = await db.PatientAccounts.SingleAsync(); // só UMA conta global
            var patients = await db.Patients.Where(p => p.PatientAccountId == account.Id).ToListAsync();
            Assert.Equal(2, patients.Count);                       // vinculado a duas clínicas
            Assert.Equal(2, patients.Select(p => p.ClinicaId).Distinct().Count());
        });
    }

    // ── 4 & 5. Bloqueio de duplicação na mesma clínica ───────────────────────

    [Fact]
    public async Task Same_email_twice_in_same_clinic_returns_conflict()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        var first = await client.PostAsJsonAsync("/api/patients", NewPatient("dup@example.com", "123.456.789-00"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/patients", NewPatient("dup@example.com", "999.888.777-66"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await app.SeedAsync(async db =>
            Assert.Equal(1, await db.Patients.CountAsync())); // sem duplicar
    }

    // ── 6. CPF global duplicado ──────────────────────────────────────────────

    [Fact]
    public async Task Same_cpf_different_email_across_clinics_returns_conflict()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            await SeedClinicAsync(db, "Clinica A", "admin-a@test.local");
            await SeedClinicAsync(db, "Clinica B", "admin-b@test.local");
        });

        using (var clientA = await LoginAsync(app, "admin-a@test.local"))
        {
            var r = await clientA.PostAsJsonAsync("/api/patients", NewPatient("pessoa1@example.com", "555.666.777-88"));
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        using var clientB = await LoginAsync(app, "admin-b@test.local");
        var response = await clientB.PostAsJsonAsync("/api/patients", NewPatient("pessoa2@example.com", "555.666.777-88"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── E-mail obrigatório ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_without_email_is_rejected()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        var response = await client.PostAsJsonAsync("/api/patients",
            new { name = "Sem Email", cpf = "123.456.789-00", phone = "(11) 99999-9999" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── 7. Paciente legado sem PatientAccountId permanece funcional ──────────

    [Fact]
    public async Task Legacy_patient_without_account_is_still_readable()
    {
        await using var app = new MultiClinicaFactory();
        int legacyId = 0;
        await app.SeedAsync(async db =>
        {
            await SeedClinicAsync(db, "Clinica A", "admin-a@test.local");
            var clinic = await db.Clinicas.SingleAsync();
            var legacy = new Patient { ClinicaId = clinic.Id, Name = "Legado", Email = "legado@example.com" };
            db.Patients.Add(legacy);
            await db.SaveChangesAsync();
            legacyId = legacy.Id;
        });

        using var client = await LoginAsync(app, "admin-a@test.local");
        var response = await client.GetAsync($"/api/patients/{legacyId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── 8. Provisionamento de acesso de paciente legado ──────────────────────

    [Fact]
    public async Task Provision_portal_access_creates_account_for_legacy_patient()
    {
        await using var app = new MultiClinicaFactory();
        int legacyId = 0;
        await app.SeedAsync(async db =>
        {
            await SeedClinicAsync(db, "Clinica A", "admin-a@test.local");
            var clinic = await db.Clinicas.SingleAsync();
            var legacy = new Patient { ClinicaId = clinic.Id, Name = "Legado", Email = "Legado@Example.com", CPF = "12345678900" };
            db.Patients.Add(legacy);
            await db.SaveChangesAsync();
            legacyId = legacy.Id;
        });

        using var client = await LoginAsync(app, "admin-a@test.local");
        var response = await client.PostAsync($"/api/patients/{legacyId}/portal-access", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>(Json);
        Assert.Equal(PatientPortalLinkResult.CreatedAccount, body!.LinkResult);
        Assert.Equal(PatientAccountStatus.PendingActivation, body.PatientAccountStatus);

        await app.SeedAsync(async db =>
        {
            var patient = await db.Patients.SingleAsync(p => p.Id == legacyId);
            Assert.NotNull(patient.PatientAccountId);
            Assert.Equal("legado@example.com", patient.Email); // normalizado
        });

        // Idempotência: segunda chamada é conflito (já provisionado).
        var again = await client.PostAsync($"/api/patients/{legacyId}/portal-access", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    // ── 9. Isolamento entre clínicas ─────────────────────────────────────────

    [Fact]
    public async Task Patient_from_other_clinic_is_not_visible()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            await SeedClinicAsync(db, "Clinica A", "admin-a@test.local");
            await SeedClinicAsync(db, "Clinica B", "admin-b@test.local");
        });

        int patientAId;
        using (var clientA = await LoginAsync(app, "admin-a@test.local"))
        {
            var r = await clientA.PostAsJsonAsync("/api/patients", NewPatient("isola@example.com", "123.456.789-00"));
            var created = await r.Content.ReadFromJsonAsync<CreatedResponse>(Json);
            patientAId = created!.PatientId;
        }

        using var clientB = await LoginAsync(app, "admin-b@test.local");
        var response = await clientB.GetAsync($"/api/patients/{patientAId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
