using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class AuthLogoutTests
{
    private const string Password = "secret123";

    [Fact]
    public async Task Logout_without_session_expires_only_clinic_cookie()
    {
        await using var app = new MultiClinicaFactory();
        using var client = app.CreateClient();

        var response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(setCookie, value => value.StartsWith("auth_token=", StringComparison.Ordinal));
        Assert.DoesNotContain(setCookie, value => value.StartsWith("patient_auth_token=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Clinic_logout_invalidates_clinic_session_and_preserves_patient_session()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica", NomeResponsavel = "Responsável" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = "admin-logout@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Administrador,
            });
            db.PatientAccounts.Add(new PatientAccount
            {
                Name = "Paciente",
                Email = "patient-logout@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Status = PatientAccountStatus.Active,
                ActivatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin-logout@test.local",
            password = Password,
        })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/patient-auth/login", new
        {
            email = "patient-logout@test.local",
            password = Password,
        })).EnsureSuccessStatusCode();

        (await client.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/patient-auth/me")).StatusCode);
    }
}
