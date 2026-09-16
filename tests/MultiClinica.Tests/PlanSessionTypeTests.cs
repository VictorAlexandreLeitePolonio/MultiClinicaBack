using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Plans;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PlanSessionTypeTests
{
    [Fact]
    public async Task Create_TipoDaClinica_RetornaIdENome()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.EmailA);

        var response = await client.PostAsJsonAsync("/api/plans", new
        {
            name = "Plano",
            valor = 100,
            tipoPlano = "Mensal",
            tipoSessaoId = seed.SessionTypeAId
        });
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(seed.SessionTypeAId, body.GetProperty("tipoSessaoId").GetInt32());
        Assert.Equal("Consulta", body.GetProperty("tipoSessaoName").GetString());
    }

    [Fact]
    public async Task Create_TipoDeOutraClinica_Retorna400()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.EmailA);

        var response = await client.PostAsJsonAsync("/api/plans", new
        {
            name = "Plano",
            valor = 100,
            tipoPlano = "Mensal",
            tipoSessaoId = seed.SessionTypeBId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_FiltroPorTipoSessaoId_RetornaSomentePlanosCorrespondentes()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.EmailA);
        await client.PostAsJsonAsync("/api/plans", new { name = "Plano A", valor = 100, tipoPlano = "Mensal", tipoSessaoId = seed.SessionTypeAId });
        await client.PostAsJsonAsync("/api/plans", new { name = "Plano B", valor = 120, tipoPlano = "Mensal", tipoSessaoId = seed.OtherSessionTypeAId });

        var json = JsonDocument.Parse(await client.GetStringAsync($"/api/plans?tipoSessaoId={seed.SessionTypeAId}"));
        var data = json.RootElement.GetProperty("data");

        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal("Plano A", data[0].GetProperty("name").GetString());
    }

    private static async Task<(string EmailA, int SessionTypeAId, int OtherSessionTypeAId, int SessionTypeBId)> SeedAsync(MultiClinicaFactory app)
    {
        var result = (EmailA: "admin-a@plans.local", SessionTypeAId: 0, OtherSessionTypeAId: 0, SessionTypeBId: 0);
        await app.SeedAsync(async db =>
        {
            var clinicA = new Clinica { Nome = "Clínica A", NomeResponsavel = "A" };
            var clinicB = new Clinica { Nome = "Clínica B", NomeResponsavel = "B" };
            db.Clinicas.AddRange(clinicA, clinicB);
            await db.SaveChangesAsync();
            db.Users.Add(new User
            {
                ClinicaId = clinicA.Id,
                Name = "Admin",
                Email = result.EmailA,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            });
            var typeA = new SessionType { ClinicaId = clinicA.Id, Name = "Consulta" };
            var otherTypeA = new SessionType { ClinicaId = clinicA.Id, Name = "Retorno" };
            var typeB = new SessionType { ClinicaId = clinicB.Id, Name = "Consulta externa" };
            db.SessionTypes.AddRange(typeA, otherTypeA, typeB);
            await db.SaveChangesAsync();
            result = (result.EmailA, typeA.Id, otherTypeA.Id, typeB.Id);
        });
        return result;
    }

    private static async Task<HttpClient> LoginAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = "secret123" });
        login.EnsureSuccessStatusCode();
        return client;
    }
}
