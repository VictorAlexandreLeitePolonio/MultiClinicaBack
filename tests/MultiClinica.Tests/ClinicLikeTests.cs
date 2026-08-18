using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class ClinicLikeTests
{
    private const string Password = "senha-super-forte";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record LikeResult(int ClinicId, int LikeCount, bool LikedByMe);
    private sealed record ClinicDto(int Id, int LikeCount, bool LikedByMe);

    private static async Task<int> SeedClinicAsync(AppDbContext db, string name)
    {
        var clinic = new Clinica { Nome = name, NomeFantasia = name, NomeResponsavel = "Victor" };
        db.Clinicas.Add(clinic);
        await db.SaveChangesAsync();
        return clinic.Id;
    }

    private static async Task<int> SeedAccountAsync(AppDbContext db, string email)
    {
        var account = new PatientAccount
        {
            Name = "Paciente", Email = email, Status = PatientAccountStatus.Active,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), ActivatedAt = DateTime.UtcNow
        };
        db.PatientAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<HttpClient> LoginAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/patient-auth/login", new { email, password = Password })).EnsureSuccessStatusCode();
        return client;
    }

    // ── Testes ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task First_like_increments_counter()
    {
        await using var app = new MultiClinicaFactory();
        int clinicId = 0;
        await app.SeedAsync(async db =>
        {
            clinicId = await SeedClinicAsync(db, "Clinica A");
            await SeedAccountAsync(db, "a@example.com");
        });

        using var client = await LoginAsync(app, "a@example.com");
        var response = await client.PostAsync($"/api/patient/clinics/{clinicId}/like", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LikeResult>(Json);
        Assert.Equal(1, result!.LikeCount);
        Assert.True(result.LikedByMe);
    }

    [Fact]
    public async Task Second_like_from_same_patient_does_not_duplicate()
    {
        await using var app = new MultiClinicaFactory();
        int clinicId = 0;
        await app.SeedAsync(async db =>
        {
            clinicId = await SeedClinicAsync(db, "Clinica A");
            await SeedAccountAsync(db, "a@example.com");
        });

        using var client = await LoginAsync(app, "a@example.com");
        await client.PostAsync($"/api/patient/clinics/{clinicId}/like", null);
        var second = await client.PostAsync($"/api/patient/clinics/{clinicId}/like", null);

        var result = await second.Content.ReadFromJsonAsync<LikeResult>(Json);
        Assert.Equal(1, result!.LikeCount);
        await app.SeedAsync(async db =>
        {
            Assert.Equal(1, await db.ClinicLikes.CountAsync());
            var clinic = await db.Clinicas.SingleAsync(c => c.Id == clinicId);
            Assert.Equal(1, clinic.LikeCount); // contador consistente com registros
        });
    }

    [Fact]
    public async Task Different_patients_increment_separately()
    {
        await using var app = new MultiClinicaFactory();
        int clinicId = 0;
        await app.SeedAsync(async db =>
        {
            clinicId = await SeedClinicAsync(db, "Clinica A");
            await SeedAccountAsync(db, "a@example.com");
            await SeedAccountAsync(db, "b@example.com");
        });

        using var ca = await LoginAsync(app, "a@example.com");
        using var cb = await LoginAsync(app, "b@example.com");
        await ca.PostAsync($"/api/patient/clinics/{clinicId}/like", null);
        var last = await cb.PostAsync($"/api/patient/clinics/{clinicId}/like", null);

        var result = await last.Content.ReadFromJsonAsync<LikeResult>(Json);
        Assert.Equal(2, result!.LikeCount);
    }

    [Fact]
    public async Task Unlike_decrements_counter()
    {
        await using var app = new MultiClinicaFactory();
        int clinicId = 0;
        await app.SeedAsync(async db =>
        {
            clinicId = await SeedClinicAsync(db, "Clinica A");
            await SeedAccountAsync(db, "a@example.com");
        });

        using var client = await LoginAsync(app, "a@example.com");
        await client.PostAsync($"/api/patient/clinics/{clinicId}/like", null);
        var unlike = await client.DeleteAsync($"/api/patient/clinics/{clinicId}/like");

        var result = await unlike.Content.ReadFromJsonAsync<LikeResult>(Json);
        Assert.Equal(0, result!.LikeCount);
        Assert.False(result.LikedByMe);
    }

    [Fact]
    public async Task Unlike_without_existing_like_does_not_go_negative()
    {
        await using var app = new MultiClinicaFactory();
        int clinicId = 0;
        await app.SeedAsync(async db =>
        {
            clinicId = await SeedClinicAsync(db, "Clinica A");
            await SeedAccountAsync(db, "a@example.com");
        });

        using var client = await LoginAsync(app, "a@example.com");
        var unlike = await client.DeleteAsync($"/api/patient/clinics/{clinicId}/like");

        var result = await unlike.Content.ReadFromJsonAsync<LikeResult>(Json);
        Assert.Equal(0, result!.LikeCount);
    }

    [Fact]
    public async Task Liking_nonexistent_clinic_returns_not_found()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db => await SeedAccountAsync(db, "a@example.com"));

        using var client = await LoginAsync(app, "a@example.com");
        var response = await client.PostAsync("/api/patient/clinics/9999/like", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LikedByMe_reflects_authenticated_patient_in_portal()
    {
        await using var app = new MultiClinicaFactory();
        int clinicId = 0;
        await app.SeedAsync(async db =>
        {
            clinicId = await SeedClinicAsync(db, "Clinica A");
            var accountId = await SeedAccountAsync(db, "a@example.com");
            db.Patients.Add(new Patient { ClinicaId = clinicId, PatientAccountId = accountId, Name = "P" });
            await db.SaveChangesAsync();
        });

        using var client = await LoginAsync(app, "a@example.com");
        await client.PostAsync($"/api/patient/clinics/{clinicId}/like", null);

        var clinics = await client.GetFromJsonAsync<List<ClinicDto>>("/api/patient/clinics", Json);
        var mine = clinics!.Single(c => c.Id == clinicId);
        Assert.True(mine.LikedByMe);
        Assert.Equal(1, mine.LikeCount);
    }
}
