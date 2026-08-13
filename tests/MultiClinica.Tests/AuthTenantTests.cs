using System.Net.Http.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class AuthTenantTests
{
    private static async Task<Clinica> SeedClinicUserAsync(MultiClinicaFactory app, string email)
    {
        Clinica clinica = null!;
        await app.SeedAsync(async db =>
        {
            clinica = new Clinica
            {
                Nome = "Clinica São Lucas LTDA",
                NomeFantasia = "Clínica São Lucas",
                NomeResponsavel = "Victor",
                Email = "contato@saolucas.com",
                Telefone = "(15) 3333-3333"
            };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinica.Id,
                Name = "Admin Clínica",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            });
            await db.SaveChangesAsync();
        });
        return clinica;
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = "secret123" });

    [Fact]
    public async Task Login_ClinicUser_ReturnsTenant()
    {
        await using var app = new MultiClinicaFactory();
        var clinica = await SeedClinicUserAsync(app, "admin.tenant1@a.local");
        using var client = app.CreateClient();

        var response = await LoginAsync(client, "admin.tenant1@a.local");
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        response.EnsureSuccessStatusCode();
        Assert.NotNull(body!.Tenant);
        Assert.Equal(clinica.Id, body.Tenant!.Id);
        Assert.Equal("Clínica São Lucas", body.Tenant.DisplayName);
        Assert.Equal("contato@saolucas.com", body.Tenant.ContactEmail);
        Assert.Equal("Administrador", body.User.Role);
        Assert.Contains(body.Permissions, p => p == "clinic.settings.view");
    }

    [Fact]
    public async Task Me_ClinicUser_ReturnsTenant()
    {
        await using var app = new MultiClinicaFactory();
        var clinica = await SeedClinicUserAsync(app, "admin.tenant2@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.tenant2@a.local");

        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        response.EnsureSuccessStatusCode();
        Assert.NotNull(body!.Tenant);
        Assert.Equal(clinica.Id, body.Tenant!.Id);
    }

    [Fact]
    public async Task Login_SuperAdmin_ReturnsNullTenant()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Admin Interno", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinica.Id,
                Name = "Root",
                Email = "root.tenant@a.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.SuperAdmin
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var response = await LoginAsync(client, "root.tenant@a.local");
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        response.EnsureSuccessStatusCode();
        Assert.Null(body!.Tenant);
    }
}
