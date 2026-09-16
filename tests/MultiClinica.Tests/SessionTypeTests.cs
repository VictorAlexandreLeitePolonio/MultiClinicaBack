using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.SessionTypes;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class SessionTypeTests
{
    [Fact]
    public async Task Create_AdminNomeComEspacos_NormalizaERetorna201()
    {
        await using var app = new MultiClinicaFactory();
        using var client = await CreateAuthenticatedClientAsync(app, "admin@clinic.local", UserRole.Administrador);

        var response = await client.PostAsJsonAsync("/api/session-types", new CreateSessionTypeDto { Name = "  Acupuntura  " });
        var body = await response.Content.ReadFromJsonAsync<SessionTypeResponseDto>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Acupuntura", body!.Name);
    }

    [Fact]
    public async Task Create_NomeDuplicadoIgnorandoCaixa_Retorna400()
    {
        await using var app = new MultiClinicaFactory();
        using var client = await CreateAuthenticatedClientAsync(app, "admin@clinic.local", UserRole.Administrador);
        await client.PostAsJsonAsync("/api/session-types", new CreateSessionTypeDto { Name = "Consulta" });

        var response = await client.PostAsJsonAsync("/api/session-types", new CreateSessionTypeDto { Name = " consulta " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_SearchEResolveTenant_RetornaSomenteCorrespondenciasDaClinica()
    {
        await using var app = new MultiClinicaFactory();
        using var clientA = await CreateAuthenticatedClientAsync(app, "admin-a@clinic.local", UserRole.Administrador, "Clínica A");
        using var clientB = await CreateAuthenticatedClientAsync(app, "admin-b@clinic.local", UserRole.Administrador, "Clínica B");
        await clientA.PostAsJsonAsync("/api/session-types", new CreateSessionTypeDto { Name = "Consulta" });
        await clientA.PostAsJsonAsync("/api/session-types", new CreateSessionTypeDto { Name = "Pilates" });
        await clientB.PostAsJsonAsync("/api/session-types", new CreateSessionTypeDto { Name = "Consulta externa" });

        var result = await clientA.GetFromJsonAsync<List<SessionTypeResponseDto>>("/api/session-types?search=cons");

        Assert.Collection(result!, item => Assert.Equal("Consulta", item.Name));
    }

    [Fact]
    public async Task Create_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        using var client = await CreateAuthenticatedClientAsync(app, "recepcao@clinic.local", UserRole.Recepcao);

        var response = await client.PostAsJsonAsync("/api/session-types", new CreateSessionTypeDto { Name = "Consulta" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        MultiClinicaFactory app,
        string email,
        UserRole role,
        string clinicName = "Clínica")
    {
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = clinicName, NomeResponsavel = "Responsável" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Usuário",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = role
            });
            await db.SaveChangesAsync();
        });

        var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = "secret123" });
        login.EnsureSuccessStatusCode();
        return client;
    }
}
