using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class FornecedorTests
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
                    Email = "admin.fornecedor@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep",
                    Email = "recep.fornecedor@a.local",
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
    public async Task CriarFornecedor_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.fornecedor@a.local");

        var response = await client.PostAsJsonAsync("/api/fornecedores",
            new CreateFornecedorDto { Nome = "Distribuidora X" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetFornecedores_Admin_Retorna200()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.fornecedor@a.local");
        await client.PostAsJsonAsync("/api/fornecedores", new CreateFornecedorDto { Nome = "Distribuidora X" });

        var response = await client.GetAsync("/api/fornecedores");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_FornecedorExistente_AtualizaNome()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.fornecedor@a.local");

        var createResponse = await client.PostAsJsonAsync("/api/fornecedores",
            new CreateFornecedorDto { Nome = "Distribuidora X" });
        var fornecedor = await createResponse.Content.ReadFromJsonAsync<FornecedorResponseDto>();

        var response = await client.PutAsJsonAsync($"/api/fornecedores/{fornecedor!.Id}",
            new UpdateFornecedorDto { Nome = "Distribuidora Y" });
        var updated = await response.Content.ReadFromJsonAsync<FornecedorResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Distribuidora Y", updated!.Nome);
    }

    [Fact]
    public async Task Inativar_FornecedorExistente_MarcaIsActiveFalse()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.fornecedor@a.local");

        var createResponse = await client.PostAsJsonAsync("/api/fornecedores",
            new CreateFornecedorDto { Nome = "Distribuidora X" });
        var fornecedor = await createResponse.Content.ReadFromJsonAsync<FornecedorResponseDto>();

        var response = await client.PostAsync($"/api/fornecedores/{fornecedor!.Id}/inativar", null);
        var updated = await response.Content.ReadFromJsonAsync<FornecedorResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task Reativar_FornecedorInativo_MarcaIsActiveTrue()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.fornecedor@a.local");

        var createResponse = await client.PostAsJsonAsync("/api/fornecedores",
            new CreateFornecedorDto { Nome = "Distribuidora X" });
        var fornecedor = await createResponse.Content.ReadFromJsonAsync<FornecedorResponseDto>();
        await client.PostAsync($"/api/fornecedores/{fornecedor!.Id}/inativar", null);

        var response = await client.PostAsync($"/api/fornecedores/{fornecedor.Id}/reativar", null);
        var updated = await response.Content.ReadFromJsonAsync<FornecedorResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(updated!.IsActive);
    }
}
