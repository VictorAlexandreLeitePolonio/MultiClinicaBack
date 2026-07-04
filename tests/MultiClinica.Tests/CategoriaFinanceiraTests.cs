using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class CategoriaFinanceiraTests
{
    private static async Task SeedAsync(MultiClinicaFactory app)
    {
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            db.Users.AddRange(
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Admin",
                    Email = "admin@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep",
                    Email = "recep@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Recepcao
                });
            await db.SaveChangesAsync();
        });
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_AdminReceitaValida_Retorna201()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");

        var response = await client.PostAsJsonAsync("/api/categorias-financeiras",
            new CreateCategoriaFinanceiraDto { Nome = "Consulta", Tipo = TipoCategoriaFinanceira.Receita });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_MesmoNomeTiposDiferentes_Permite()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");
        await client.PostAsJsonAsync("/api/categorias-financeiras",
            new CreateCategoriaFinanceiraDto { Nome = "Ajuste", Tipo = TipoCategoriaFinanceira.Receita });

        var response = await client.PostAsJsonAsync("/api/categorias-financeiras",
            new CreateCategoriaFinanceiraDto { Nome = "Ajuste", Tipo = TipoCategoriaFinanceira.Despesa });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep@a.local");

        var response = await client.PostAsJsonAsync("/api/categorias-financeiras",
            new CreateCategoriaFinanceiraDto { Nome = "Consulta", Tipo = TipoCategoriaFinanceira.Receita });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
