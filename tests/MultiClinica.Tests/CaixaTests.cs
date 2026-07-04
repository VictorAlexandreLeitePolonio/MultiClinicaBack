using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class CaixaTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<int> SeedAsync(MultiClinicaFactory app)
    {
        var contaFinanceiraId = 0;
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            var conta = new ContaFinanceira { ClinicaId = clinica.Id, Nome = "Caixa Loja", Tipo = TipoContaFinanceira.Caixa };
            db.ContasFinanceiras.Add(conta);

            db.Users.AddRange(
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Admin",
                    Email = "admin.caixa@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep",
                    Email = "recep.caixa@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Recepcao
                });
            await db.SaveChangesAsync();

            contaFinanceiraId = conta.Id;
        });
        return contaFinanceiraId;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<CaixaResponseDto> AbrirAsync(HttpClient client, int contaFinanceiraId, decimal saldoInicial = 100m)
    {
        var response = await client.PostAsJsonAsync("/api/caixa/abrir",
            new AbrirCaixaDto { ContaFinanceiraId = contaFinanceiraId, SaldoInicial = saldoInicial });
        return (await response.Content.ReadFromJsonAsync<CaixaResponseDto>(JsonOptions))!;
    }

    [Fact]
    public async Task AbrirCaixa_JaExisteAberto_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.caixa@a.local");
        await AbrirAsync(client, contaFinanceiraId);

        var response = await client.PostAsJsonAsync("/api/caixa/abrir",
            new AbrirCaixaDto { ContaFinanceiraId = contaFinanceiraId, SaldoInicial = 50m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AbrirCaixa_Recepcao_Retorna201()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.caixa@a.local");

        var response = await client.PostAsJsonAsync("/api/caixa/abrir",
            new AbrirCaixaDto { ContaFinanceiraId = contaFinanceiraId, SaldoInicial = 100m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task FecharCaixa_SemMovimentacoes_CalculaSaldoEDiferenca()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.caixa@a.local");
        var caixa = await AbrirAsync(client, contaFinanceiraId, 100m);

        var response = await client.PostAsJsonAsync($"/api/caixa/{caixa.Id}/fechar",
            new FecharCaixaDto { SaldoFinalInformado = 90m });
        var fechado = await response.Content.ReadFromJsonAsync<CaixaResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100m, fechado!.SaldoFinalCalculado);
        Assert.Equal(-10m, fechado.Diferenca);
    }

    [Fact]
    public async Task FecharCaixa_ComMovimentacoes_CalculaSaldo()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.caixa@a.local");
        var caixa = await AbrirAsync(client, contaFinanceiraId, 100m);
        await app.SeedAsync(async db =>
        {
            var entity = await db.Caixas.FindAsync(caixa.Id);
            db.MovimentacoesFinanceiras.AddRange(
                new MovimentacaoFinanceira
                {
                    ClinicaId = entity!.ClinicaId,
                    ContaFinanceiraId = contaFinanceiraId,
                    Tipo = TipoMovimentacaoFinanceira.Entrada,
                    Origem = OrigemMovimentacaoFinanceira.Recebimento,
                    Descricao = "Entrada",
                    Valor = 40,
                    DataMovimentacao = caixa.DataAbertura.AddTicks(1)
                },
                new MovimentacaoFinanceira
                {
                    ClinicaId = entity.ClinicaId,
                    ContaFinanceiraId = contaFinanceiraId,
                    Tipo = TipoMovimentacaoFinanceira.Saida,
                    Origem = OrigemMovimentacaoFinanceira.Estorno,
                    Descricao = "Saida",
                    Valor = 10,
                    DataMovimentacao = caixa.DataAbertura.AddTicks(2)
                });
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync($"/api/caixa/{caixa.Id}/fechar",
            new FecharCaixaDto { SaldoFinalInformado = 130m });
        var fechado = await response.Content.ReadFromJsonAsync<CaixaResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(130m, fechado!.SaldoFinalCalculado);
        Assert.Equal(0m, fechado.Diferenca);
    }

    [Fact]
    public async Task ReabrirCaixa_SemMotivo_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.caixa@a.local");
        var caixa = await AbrirAsync(client, contaFinanceiraId);
        await client.PostAsJsonAsync($"/api/caixa/{caixa.Id}/fechar", new FecharCaixaDto { SaldoFinalInformado = 100m });

        var response = await client.PostAsJsonAsync($"/api/caixa/{caixa.Id}/reabrir", new MotivoDto { Motivo = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReabrirCaixa_ComMotivo_VoltaParaAberto()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.caixa@a.local");
        var caixa = await AbrirAsync(client, contaFinanceiraId);
        await client.PostAsJsonAsync($"/api/caixa/{caixa.Id}/fechar", new FecharCaixaDto { SaldoFinalInformado = 100m });

        var response = await client.PostAsJsonAsync($"/api/caixa/{caixa.Id}/reabrir",
            new MotivoDto { Motivo = "Esqueci de lançar uma venda" });
        var reaberto = await response.Content.ReadFromJsonAsync<CaixaResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StatusCaixa.Aberto, reaberto!.Status);
        Assert.Null(reaberto.SaldoFinalCalculado);
    }

    [Fact]
    public async Task AjustarCaixa_CaixaAberto_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.caixa@a.local");
        var caixa = await AbrirAsync(client, contaFinanceiraId);

        var response = await client.PostAsJsonAsync($"/api/caixa/{caixa.Id}/ajustar",
            new AjustarCaixaDto { SaldoFinalInformado = 90m, Motivo = "Correção" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AjustarCaixa_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var adminClient = app.CreateClient();
        await LoginAsync(adminClient, "admin.caixa@a.local");
        var caixa = await AbrirAsync(adminClient, contaFinanceiraId);
        await adminClient.PostAsJsonAsync($"/api/caixa/{caixa.Id}/fechar", new FecharCaixaDto { SaldoFinalInformado = 100m });

        using var recepClient = app.CreateClient();
        await LoginAsync(recepClient, "recep.caixa@a.local");

        var response = await recepClient.PostAsJsonAsync($"/api/caixa/{caixa.Id}/ajustar",
            new AjustarCaixaDto { SaldoFinalInformado = 90m, Motivo = "Correção" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelarCaixa_ComMotivo_MudaStatusParaCancelado()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.caixa@a.local");
        var caixa = await AbrirAsync(client, contaFinanceiraId);

        var response = await client.PostAsJsonAsync($"/api/caixa/{caixa.Id}/cancelar",
            new MotivoDto { Motivo = "Aberto por engano" });
        var cancelado = await response.Content.ReadFromJsonAsync<CaixaResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StatusCaixa.Cancelado, cancelado!.Status);
    }

    [Fact]
    public async Task CancelarCaixa_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var adminClient = app.CreateClient();
        await LoginAsync(adminClient, "admin.caixa@a.local");
        var caixa = await AbrirAsync(adminClient, contaFinanceiraId);

        using var recepClient = app.CreateClient();
        await LoginAsync(recepClient, "recep.caixa@a.local");

        var response = await recepClient.PostAsJsonAsync($"/api/caixa/{caixa.Id}/cancelar",
            new MotivoDto { Motivo = "Teste" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMovimentacoes_Recepcao_Retorna200()
    {
        await using var app = new MultiClinicaFactory();
        var contaFinanceiraId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.caixa@a.local");
        var caixa = await AbrirAsync(client, contaFinanceiraId);

        var response = await client.GetAsync($"/api/caixa/{caixa.Id}/movimentacoes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
