using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.User;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class RequireFeatureTests
{
    private static MultiClinicaFactory CreateApp() => new()
    {
        ConfigureTestServices = s =>
        {
            s.AddSingleton<IWhatsAppClient>(new FakeWhatsAppClient());
            s.Configure<WhatsAppOptions>(o => o.DisplayNumber = "5511900000000");
        },
    };

    private static async Task<int> SeedClinicAsync(MultiClinicaFactory app, ClinicPlan plan = ClinicPlan.Free,
        int extraProfessionals = 0, int professionals = 0)
    {
        var clinicId = 0;
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica", Plan = plan, ExtraProfessionals = extraProfessionals };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id, Name = "Admin", Email = "admin@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Administrador,
            });
            for (var i = 0; i < professionals; i++)
                db.Users.Add(new User
                {
                    ClinicaId = clinic.Id, Name = $"Profissional {i}", Email = $"pro{i}@test.local",
                    Role = UserRole.Profissional,
                });
            await db.SaveChangesAsync();
            clinicId = clinic.Id;
        });
        return clinicId;
    }

    private static async Task LoginAsync(HttpClient client)
        => (await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "admin@test.local",
            Password = "secret123",
        })).EnsureSuccessStatusCode();

    [Fact]
    public async Task WhatsApp_link_requires_owner_copilot_plan()
    {
        await using var app = CreateApp();
        await SeedClinicAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client);

        var response = await client.PostAsync("/api/me/whatsapp-link", null);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PLAN_REQUIRED", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("OwnerCopilot", body.RootElement.GetProperty("feature").GetString());
        Assert.Equal("Pro", body.RootElement.GetProperty("requiredPlan").GetString());
    }

    [Fact]
    public async Task Upgrade_to_pro_unlocks_whatsapp_link_without_new_login()
    {
        await using var app = CreateApp();
        await SeedClinicAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.PaymentRequired,
            (await client.PostAsync("/api/me/whatsapp-link", null)).StatusCode);

        await app.SeedAsync(async db =>
        {
            var clinic = await db.Clinicas.SingleAsync();
            clinic.Plan = ClinicPlan.Pro;
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsync("/api/me/whatsapp-link", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.StartsWith("https://wa.me/5511900000000?text=", body.RootElement.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Invite_professional_is_blocked_at_the_plan_limit()
    {
        await using var app = CreateApp();
        await SeedClinicAsync(app, professionals: 1);
        using var client = app.CreateClient();
        await LoginAsync(client);

        var response = await client.PostAsJsonAsync("/api/user-invitations", new InviteUserDto
        {
            Name = "Novo Profissional",
            Email = "novo@test.local",
            Role = UserRole.Profissional,
        });

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PROFESSIONAL_LIMIT", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task Extra_professional_slot_allows_invite()
    {
        await using var app = CreateApp();
        await SeedClinicAsync(app, extraProfessionals: 1, professionals: 1);
        using var client = app.CreateClient();
        await LoginAsync(client);

        var response = await client.PostAsJsonAsync("/api/user-invitations", new InviteUserDto
        {
            Name = "Novo Profissional",
            Email = "novo@test.local",
            Role = UserRole.Profissional,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reception_invite_is_not_limited_by_the_plan()
    {
        await using var app = CreateApp();
        await SeedClinicAsync(app, professionals: 1);
        using var client = app.CreateClient();
        await LoginAsync(client);

        var response = await client.PostAsJsonAsync("/api/user-invitations", new InviteUserDto
        {
            Name = "Recepção",
            Email = "recepcao@test.local",
            Role = UserRole.Recepcao,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
