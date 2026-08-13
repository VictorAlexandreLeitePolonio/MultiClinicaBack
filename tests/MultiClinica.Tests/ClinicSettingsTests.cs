using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Clinic;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class ClinicSettingsTests
{
    private static async Task<Clinica> SeedClinicUserAsync(MultiClinicaFactory app, string clinicName, string email, UserRole role = UserRole.Administrador)
    {
        Clinica clinica = null!;
        await app.SeedAsync(async db =>
        {
            clinica = new Clinica { Nome = clinicName, NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinica.Id,
                Name = "User",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = role
            });
            await db.SaveChangesAsync();
        });
        return clinica;
    }

    private static async Task LoginAsync(HttpClient client, string email) =>
        (await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = "secret123" }))
            .EnsureSuccessStatusCode();

    [Fact]
    public async Task GetClinicSettings_CurrentClinic_ReturnsSettings()
    {
        await using var app = new MultiClinicaFactory();
        var clinica = await SeedClinicUserAsync(app, "Clinica Settings 1", "admin.settings1@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.settings1@a.local");

        var response = await client.GetAsync("/api/clinic/settings");
        var settings = await response.Content.ReadFromJsonAsync<ClinicSettingsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(clinica.Id, settings!.ClinicId);
    }

    [Fact]
    public async Task UpdateClinicSettings_ValidPayload_UpdatesSettings()
    {
        await using var app = new MultiClinicaFactory();
        await SeedClinicUserAsync(app, "Clinica Settings 2", "admin.settings2@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.settings2@a.local");

        var response = await client.PutAsJsonAsync("/api/clinic/settings", new UpdateClinicSettingsRequest
        {
            DisplayName = "Nome de Exibição",
            LogoUrl = "https://cdn.example.com/logo.png",
            PrimaryColor = "#2563EB",
            SecondaryColor = "#0F172A",
            AccentColor = "#22C55E",
            ContactEmail = "contato@exemplo.com",
            ContactPhone = "(15) 99999-9999"
        });
        var settings = await response.Content.ReadFromJsonAsync<ClinicSettingsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Nome de Exibição", settings!.DisplayName);
        Assert.Equal("#2563EB", settings.PrimaryColor);
        Assert.Equal("contato@exemplo.com", settings.ContactEmail);
    }

    [Fact]
    public async Task UpdateClinicSettings_InvalidColor_ReturnsBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        await SeedClinicUserAsync(app, "Clinica Settings 3", "admin.settings3@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.settings3@a.local");

        var response = await client.PutAsJsonAsync("/api/clinic/settings", new UpdateClinicSettingsRequest
        {
            PrimaryColor = "azul"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateClinicSettings_DoesNotAcceptClinicId()
    {
        await using var app = new MultiClinicaFactory();
        var clinicaA = await SeedClinicUserAsync(app, "Clinica A Settings", "admin.settings4a@a.local");
        var clinicaB = await SeedClinicUserAsync(app, "Clinica B Settings", "admin.settings4b@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.settings4a@a.local");

        // Envia clinicId no corpo (campo inexistente no DTO) tentando mirar a clínica B — deve ser ignorado.
        var response = await client.PutAsJsonAsync("/api/clinic/settings", new
        {
            clinicId = clinicaB.Id,
            displayName = "Tentativa de invasão"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settingsA = await (await client.GetAsync("/api/clinic/settings")).Content.ReadFromJsonAsync<ClinicSettingsDto>();
        Assert.Equal(clinicaA.Id, settingsA!.ClinicId);
        Assert.Equal("Tentativa de invasão", settingsA.DisplayName);
    }

    [Fact]
    public async Task UpdateClinicSettings_UserFromClinicA_DoesNotUpdateClinicB()
    {
        await using var app = new MultiClinicaFactory();
        var clinicaA = await SeedClinicUserAsync(app, "Clinica A Isolada", "admin.settings5a@a.local");
        var clinicaB = await SeedClinicUserAsync(app, "Clinica B Isolada", "admin.settings5b@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.settings5a@a.local");

        await client.PutAsJsonAsync("/api/clinic/settings", new UpdateClinicSettingsRequest
        {
            DisplayName = "Alterado pela Clínica A"
        });

        using var clientB = app.CreateClient();
        await LoginAsync(clientB, "admin.settings5b@a.local");
        var settingsB = await (await clientB.GetAsync("/api/clinic/settings")).Content.ReadFromJsonAsync<ClinicSettingsDto>();

        Assert.Equal(clinicaB.Id, settingsB!.ClinicId);
        Assert.NotEqual("Alterado pela Clínica A", settingsB.DisplayName);
    }

    [Fact]
    public async Task SuperAdminUpdateClinicSettings_ValidPayload_UpdatesTargetClinic()
    {
        await using var app = new MultiClinicaFactory();
        var targetClinic = await SeedClinicUserAsync(app, "Clinica Alvo", "admin.settings6@a.local");
        await app.SeedAsync(async db =>
        {
            var superAdminClinic = new Clinica { Nome = "Admin Interno", NomeResponsavel = "Victor" };
            db.Clinicas.Add(superAdminClinic);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = superAdminClinic.Id,
                Name = "Root",
                Email = "root.settings@a.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.SuperAdmin
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "root.settings@a.local");

        var response = await client.PutAsJsonAsync($"/api/superadmin/clinics/{targetClinic.Id}/settings", new UpdateClinicSettingsRequest
        {
            DisplayName = "Definido pelo SuperAdmin"
        });
        var settings = await response.Content.ReadFromJsonAsync<ClinicSettingsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(targetClinic.Id, settings!.ClinicId);
        Assert.Equal("Definido pelo SuperAdmin", settings.DisplayName);
    }
}
