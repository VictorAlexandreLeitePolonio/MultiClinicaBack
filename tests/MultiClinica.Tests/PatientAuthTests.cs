using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PatientAuthTests
{
    private const string Password = "senha-super-forte";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PatientAccount NewAccount(string email, PatientAccountStatus status, bool withPassword = true) =>
        new()
        {
            Name = "Paciente",
            Email = email,
            CPF = "12345678900",
            Status = status,
            PasswordHash = withPassword ? BCrypt.Net.BCrypt.HashPassword(Password) : null,
            ActivatedAt = status == PatientAccountStatus.Active ? DateTime.UtcNow : null
        };

    private static async Task<int> SeedTokenAsync(AppDbContext db, int accountId, PatientAuthTokenType type,
        string rawToken, DateTime expiresAt, DateTime? consumedAt = null)
    {
        var token = new PatientAuthToken
        {
            PatientAccountId = accountId,
            Type = type,
            TokenHash = HashToken(rawToken),
            ExpiresAt = expiresAt,
            ConsumedAt = consumedAt
        };
        db.PatientAuthTokens.Add(token);
        await db.SaveChangesAsync();
        return token.Id;
    }

    private static string HashToken(string raw)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));

    // ── login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Active_account_can_login()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            db.PatientAccounts.Add(NewAccount("ativo@example.com", PatientAccountStatus.Active));
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/patient-auth/login",
            new { email = "ativo@example.com", password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("patient_auth_token", CookieHeader(response));
    }

    [Fact]
    public async Task Pending_account_cannot_login()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            db.PatientAccounts.Add(NewAccount("pendente@example.com", PatientAccountStatus.PendingActivation));
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/patient-auth/login",
            new { email = "pendente@example.com", password = Password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_account_cannot_login()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            db.PatientAccounts.Add(NewAccount("inativo@example.com", PatientAccountStatus.Inactive));
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/patient-auth/login",
            new { email = "inativo@example.com", password = Password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── activate ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Valid_activation_token_activates_account()
    {
        await using var app = new MultiClinicaFactory();
        int accountId = 0;
        await app.SeedAsync(async db =>
        {
            var account = NewAccount("novo@example.com", PatientAccountStatus.PendingActivation, withPassword: false);
            db.PatientAccounts.Add(account);
            await db.SaveChangesAsync();
            accountId = account.Id;
            await SeedTokenAsync(db, accountId, PatientAuthTokenType.Activation, "raw-activation", DateTime.UtcNow.AddHours(24));
        });

        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/patient-auth/activate",
            new { token = "raw-activation", password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var account = await db.PatientAccounts.SingleAsync(a => a.Id == accountId);
            Assert.Equal(PatientAccountStatus.Active, account.Status);
            Assert.NotNull(account.PasswordHash);
            Assert.NotNull(account.ActivatedAt);
            var token = await db.PatientAuthTokens.SingleAsync();
            Assert.NotNull(token.ConsumedAt); // uso único
        });
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var account = NewAccount("exp@example.com", PatientAccountStatus.PendingActivation, withPassword: false);
            db.PatientAccounts.Add(account);
            await db.SaveChangesAsync();
            await SeedTokenAsync(db, account.Id, PatientAuthTokenType.Activation, "raw-expired", DateTime.UtcNow.AddHours(-1));
        });

        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/patient-auth/activate",
            new { token = "raw-expired", password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Consumed_token_is_rejected()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var account = NewAccount("used@example.com", PatientAccountStatus.PendingActivation, withPassword: false);
            db.PatientAccounts.Add(account);
            await db.SaveChangesAsync();
            await SeedTokenAsync(db, account.Id, PatientAuthTokenType.Activation, "raw-used",
                DateTime.UtcNow.AddHours(24), consumedAt: DateTime.UtcNow.AddMinutes(-5));
        });

        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/patient-auth/activate",
            new { token = "raw-used", password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── reset de senha ───────────────────────────────────────────────────────

    [Fact]
    public async Task Reset_password_updates_hash_and_consumes_token()
    {
        await using var app = new MultiClinicaFactory();
        int accountId = 0;
        string originalHash = "";
        await app.SeedAsync(async db =>
        {
            var account = NewAccount("reset@example.com", PatientAccountStatus.Active);
            db.PatientAccounts.Add(account);
            await db.SaveChangesAsync();
            accountId = account.Id;
            originalHash = account.PasswordHash!;
            await SeedTokenAsync(db, accountId, PatientAuthTokenType.PasswordReset, "raw-reset", DateTime.UtcNow.AddHours(1));
        });

        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/patient-auth/reset-password",
            new { token = "raw-reset", password = "nova-senha-forte" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var account = await db.PatientAccounts.SingleAsync(a => a.Id == accountId);
            Assert.NotEqual(originalHash, account.PasswordHash);
            var token = await db.PatientAuthTokens.SingleAsync();
            Assert.NotNull(token.ConsumedAt);
        });
    }

    // ── forgot-password (sem enumeração) ─────────────────────────────────────

    [Fact]
    public async Task Forgot_password_does_not_reveal_whether_email_exists()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            db.PatientAccounts.Add(NewAccount("existe@example.com", PatientAccountStatus.Active));
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var existing = await client.PostAsJsonAsync("/api/patient-auth/forgot-password", new { email = "existe@example.com" });
        var missing = await client.PostAsJsonAsync("/api/patient-auth/forgot-password", new { email = "naoexiste@example.com" });

        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Equal(await existing.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }

    // ── isolamento entre esquemas ────────────────────────────────────────────

    [Fact]
    public async Task Patient_token_cannot_access_clinic_endpoints()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            db.PatientAccounts.Add(NewAccount("cross@example.com", PatientAccountStatus.Active));
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/patient-auth/login",
            new { email = "cross@example.com", password = Password });
        login.EnsureSuccessStatusCode(); // cookie patient_auth_token setado

        var response = await client.GetAsync("/api/patients"); // endpoint da clínica
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Clinic_token_cannot_access_patient_endpoints()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = "admin@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@test.local", password = "secret123" });
        login.EnsureSuccessStatusCode(); // cookie auth_token setado

        var response = await client.GetAsync("/api/patient-auth/me"); // endpoint do paciente
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── cookie patient_auth_token e /me ──────────────────────────────────────

    [Fact]
    public async Task Login_sets_patient_cookie_and_me_returns_account()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            db.PatientAccounts.Add(NewAccount("me@example.com", PatientAccountStatus.Active));
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/patient-auth/login",
            new { email = "me@example.com", password = Password });
        login.EnsureSuccessStatusCode();
        Assert.Contains("patient_auth_token", CookieHeader(login));

        var me = await client.GetAsync("/api/patient-auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains("me@example.com", await me.Content.ReadAsStringAsync());
    }

    // ── billing da clínica não afeta o paciente ──────────────────────────────

    [Fact]
    public async Task Clinic_billing_block_does_not_affect_patient_auth()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = "Clinica Bloqueada",
                NomeResponsavel = "Victor",
                IsBlockedByBilling = true,
                BillingBlockReason = "Pendência"
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var account = NewAccount("independente@example.com", PatientAccountStatus.Active);
            db.PatientAccounts.Add(account);
            await db.SaveChangesAsync();

            // vincula o paciente à clínica bloqueada
            db.Patients.Add(new Patient
            {
                ClinicaId = clinic.Id,
                PatientAccountId = account.Id,
                Name = "Paciente",
                Email = "independente@example.com"
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/patient-auth/login",
            new { email = "independente@example.com", password = Password });
        login.EnsureSuccessStatusCode();

        var me = await client.GetAsync("/api/patient-auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode); // billing da clínica não bloqueia o paciente
    }

    private static string CookieHeader(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values) ? string.Join(";", values) : "";
}
