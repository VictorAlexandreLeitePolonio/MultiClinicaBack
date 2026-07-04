using System.Net.Http.Json;
using System.Text.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class MeEndpointTests
{
    [Fact]
    public async Task Me_Recepcao_RetornaPermissoesEsperadas()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinica.Id,
                Name = "Recep",
                Email = "recep@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Recepcao,
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = "recep@test.local", Password = "secret123" });
        login.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/auth/me");

        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var perms = doc.RootElement.GetProperty("permissions")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        Assert.Contains("financeiro.contas_receber.registrar_recebimento", perms);
        Assert.DoesNotContain("financeiro.contas_receber.estornar_recebimento", perms);
        Assert.Equal(14, perms.Count);
    }
}
