using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class AuditoriaFinanceiraTests
{
    // Clinica A recebe o admin + registros; Clinica B recebe registros que nunca podem vazar.
    private static async Task SeedAsync(MultiClinicaFactory app)
    {
        await app.SeedAsync(async db =>
        {
            var clinicaA = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            var clinicaB = new Clinica { Nome = "Clinica B", NomeResponsavel = "Outro" };
            db.Clinicas.AddRange(clinicaA, clinicaB);
            await db.SaveChangesAsync();

            var admin = new User
            {
                ClinicaId = clinicaA.Id,
                Name = "Admin A",
                Email = "admin.auditoria@a.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            };
            var recep = new User
            {
                ClinicaId = clinicaA.Id,
                Name = "Recep A",
                Email = "recep.auditoria@a.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Recepcao
            };
            db.Users.AddRange(admin, recep);
            await db.SaveChangesAsync();

            db.AuditoriasFinanceiras.AddRange(
                new AuditoriaFinanceira
                {
                    ClinicaId = clinicaA.Id,
                    UsuarioId = admin.Id,
                    Modulo = "Fornecedores",
                    Acao = "Criar",
                    Entidade = "Fornecedor",
                    EntidadeId = 1,
                    DadosDepois = "{\"Nome\":\"X\"}",
                    DataAcao = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc)
                },
                new AuditoriaFinanceira
                {
                    ClinicaId = clinicaA.Id,
                    UsuarioId = admin.Id,
                    Modulo = "Estoque",
                    Acao = "Ajuste",
                    Entidade = "Produto",
                    EntidadeId = 2,
                    DataAcao = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc)
                },
                new AuditoriaFinanceira
                {
                    ClinicaId = clinicaB.Id,
                    UsuarioId = admin.Id,
                    Modulo = "Fornecedores",
                    Acao = "Criar",
                    Entidade = "Fornecedor",
                    EntidadeId = 99,
                    DataAcao = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc)
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

    private static Task<PagedResult<AuditoriaFinanceiraDto>?> GetAsync(HttpClient client, string url) =>
        client.GetFromJsonAsync<PagedResult<AuditoriaFinanceiraDto>>(url);

    [Fact]
    public async Task Listar_Admin_Retorna200ComRegistrosDaPropriaClinica()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.auditoria@a.local");

        var response = await client.GetAsync("/api/financeiro/auditoria");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<AuditoriaFinanceiraDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, page!.TotalCount);
        Assert.All(page.Data, a => Assert.NotEqual(99, a.EntidadeId));
        // ordena por DataAcao desc
        Assert.Equal("Estoque", page.Data.First().Modulo);
        Assert.Equal("Admin A", page.Data.First().UsuarioNome);
    }

    [Fact]
    public async Task Listar_NaoRetornaRegistrosDeOutraClinica()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.auditoria@a.local");

        var page = await GetAsync(client, "/api/financeiro/auditoria?pageSize=100");

        Assert.DoesNotContain(page!.Data, a => a.EntidadeId == 99);
    }

    [Fact]
    public async Task Listar_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.auditoria@a.local");

        var response = await client.GetAsync("/api/financeiro/auditoria");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listar_FiltroModulo_RetornaSomenteDoModulo()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.auditoria@a.local");

        var page = await GetAsync(client, "/api/financeiro/auditoria?modulo=Estoque");

        Assert.Equal(1, page!.TotalCount);
        Assert.All(page.Data, a => Assert.Equal("Estoque", a.Modulo));
    }

    [Fact]
    public async Task Listar_FiltroData_RespeitaIntervalo()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.auditoria@a.local");

        var page = await GetAsync(client,
            "/api/financeiro/auditoria?dataInicio=2026-08-10&dataFim=2026-08-31");

        Assert.Equal(1, page!.TotalCount);
        Assert.Equal("Estoque", page.Data.First().Modulo);
    }

    [Fact]
    public async Task Listar_PageSizeAcimaDe100_ClampaEm100()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.auditoria@a.local");

        var page = await GetAsync(client, "/api/financeiro/auditoria?pageSize=500");

        Assert.Equal(100, page!.PageSize);
    }
}
