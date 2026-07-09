using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class AuditoriaFinanceiraTests
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
                    Email = "admin.auditoria@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep",
                    Email = "recep.auditoria@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Recepcao
                });
            await db.SaveChangesAsync();

            fornecedorId = fornecedor.Id;
        });
        return fornecedorId;
    }

    private static async Task SeedTwoClinicsAsync(MultiClinicaFactory app)
    {
        await app.SeedAsync(async db =>
        {
            var clinicaA = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            var clinicaB = new Clinica { Nome = "Clinica B", NomeResponsavel = "Ana" };
            db.Clinicas.AddRange(clinicaA, clinicaB);
            await db.SaveChangesAsync();

            db.Users.AddRange(
                new User
                {
                    ClinicaId = clinicaA.Id,
                    Name = "Admin A",
                    Email = "admin.auditoria@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinicaB.Id,
                    Name = "Admin B",
                    Email = "admin.auditoria@b.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
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
    public async Task GetAuditoria_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.auditoria@a.local");

        var response = await client.GetAsync("/api/auditoria-financeira");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CriarFornecedor_GeraAuditoriaConsultavelPeloAdmin()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.auditoria@a.local");

        var createResponse = await client.PostAsJsonAsync("/api/fornecedores",
            new CreateFornecedorDto { Nome = "Laboratorio Central" });
        createResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/auditoria-financeira?modulo=Fornecedores");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AuditoriaFinanceiraResponseDto>>(JsonOptions);
        var item = Assert.Single(body!.Data);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Fornecedores", item.Modulo);
        Assert.Equal("Criar", item.Acao);
        Assert.Equal("Fornecedor", item.Entidade);
        Assert.Contains("Laboratorio Central", item.DadosDepois);
        Assert.Null(item.DadosAntes);
    }

    [Fact]
    public async Task GetAuditoria_AdminNaoVeAuditoriaDeOutraClinica()
    {
        await using var app = new MultiClinicaFactory();
        await SeedTwoClinicsAsync(app);

        using var clinicaAClient = app.CreateClient();
        await LoginAsync(clinicaAClient, "admin.auditoria@a.local");
        var createResponse = await clinicaAClient.PostAsJsonAsync("/api/fornecedores",
            new CreateFornecedorDto { Nome = "Fornecedor Clinica A" });
        createResponse.EnsureSuccessStatusCode();

        using var clinicaBClient = app.CreateClient();
        await LoginAsync(clinicaBClient, "admin.auditoria@b.local");

        var response = await clinicaBClient.GetAsync("/api/auditoria-financeira");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AuditoriaFinanceiraResponseDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body!.Data);
        Assert.Equal(0, body.TotalCount);
    }

    [Fact]
    public async Task CancelarContaPagar_GeraAuditoriaComMotivoESnapshots()
    {
        await using var app = new MultiClinicaFactory();
        var fornecedorId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.auditoria@a.local");

        var createResponse = await client.PostAsJsonAsync("/api/contas-pagar", new CreateContaPagarDto
        {
            FornecedorId = fornecedorId,
            Descricao = "Aluguel",
            ValorOriginal = 500,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });
        var conta = await createResponse.Content.ReadFromJsonAsync<ContaPagarResponseDto>(JsonOptions);

        var cancelResponse = await client.PostAsJsonAsync($"/api/contas-pagar/{conta!.Id}/cancelar",
            new MotivoDto { Motivo = "Duplicidade" });
        cancelResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/auditoria-financeira?modulo=ContasPagar&pageSize=10");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AuditoriaFinanceiraResponseDto>>(JsonOptions);
        var cancelamento = body!.Data.Single(a => a.Acao == "Cancelar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body.TotalCount);
        Assert.Equal("Duplicidade", cancelamento.Motivo);
        Assert.Contains("\"Status\":\"Aberta\"", cancelamento.DadosAntes);
        Assert.Contains("\"Status\":\"Cancelada\"", cancelamento.DadosDepois);
    }
}
