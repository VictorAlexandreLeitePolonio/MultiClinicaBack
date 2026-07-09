using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class ContaPagarTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<int> SeedAsync(MultiClinicaFactory app)
    {
        var fornecedorId = 0;
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            var fornecedor = new Fornecedor { ClinicaId = clinica.Id, Nome = "Distribuidora X" };
            db.Fornecedores.Add(fornecedor);

            db.Users.AddRange(
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Admin",
                    Email = "admin.contaspagar@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep",
                    Email = "recep.contaspagar@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Recepcao
                });
            await db.SaveChangesAsync();

            fornecedorId = fornecedor.Id;
        });
        return fornecedorId;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CriarContaPagar_ValorOriginalZero_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var fornecedorId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.contaspagar@a.local");

        var response = await client.PostAsJsonAsync("/api/contas-pagar", new CreateContaPagarDto
        {
            FornecedorId = fornecedorId,
            Descricao = "Aluguel",
            ValorOriginal = 0,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarContaPagar_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        var fornecedorId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.contaspagar@a.local");

        var response = await client.PostAsJsonAsync("/api/contas-pagar", new CreateContaPagarDto
        {
            FornecedorId = fornecedorId,
            Descricao = "Aluguel",
            ValorOriginal = 500,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelarConta_ComValorPago_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var fornecedorId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.contaspagar@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/contas-pagar", new CreateContaPagarDto
        {
            FornecedorId = fornecedorId,
            Descricao = "Aluguel",
            ValorOriginal = 500,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });
        var conta = await createResponse.Content.ReadFromJsonAsync<ContaPagarResponseDto>(JsonOptions);
        await app.SeedAsync(async db =>
        {
            var entity = await db.ContasPagar.FirstAsync(c => c.Id == conta!.Id);
            entity.ValorPago = 100;
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync($"/api/contas-pagar/{conta!.Id}/cancelar",
            new MotivoDto { Motivo = "Teste" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
