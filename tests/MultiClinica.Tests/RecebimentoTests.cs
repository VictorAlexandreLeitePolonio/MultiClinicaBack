using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class RecebimentoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<(int PacienteId, int ContaFinanceiraId, int FormaPagamentoId)> SeedAsync(MultiClinicaFactory app)
    {
        var ids = (PacienteId: 0, ContaFinanceiraId: 0, FormaPagamentoId: 0);
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            var paciente = new Patient { ClinicaId = clinica.Id, Name = "Paciente A" };
            db.Patients.Add(paciente);

            var conta = new ContaFinanceira { ClinicaId = clinica.Id, Nome = "Caixa", Tipo = TipoContaFinanceira.Caixa };
            db.ContasFinanceiras.Add(conta);

            var forma = new FormaPagamento { ClinicaId = clinica.Id, Nome = "Pix" };
            db.FormasPagamento.Add(forma);

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

            ids = (paciente.Id, conta.Id, forma.Id);
        });
        return ids;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ContaReceberResponseDto> CriarContaAsync(HttpClient client, int pacienteId, decimal valor)
    {
        var response = await client.PostAsJsonAsync("/api/contas-receber", new CreateContaReceberDto
        {
            PacienteId = pacienteId,
            Descricao = "Consulta",
            ValorOriginal = valor,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });
        return (await response.Content.ReadFromJsonAsync<ContaReceberResponseDto>(JsonOptions))!;
    }

    [Fact]
    public async Task RegistrarRecebimento_ValorMaiorQueSaldo_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (pacienteId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");
        var conta = await CriarContaAsync(client, pacienteId, 100);

        var response = await client.PostAsJsonAsync("/api/recebimentos", new CreateRecebimentoDto
        {
            ContaReceberId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 150,
            DataRecebimento = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegistrarRecebimento_ValorExato_MudaStatusParaPaga()
    {
        await using var app = new MultiClinicaFactory();
        var (pacienteId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");
        var conta = await CriarContaAsync(client, pacienteId, 100);

        var response = await client.PostAsJsonAsync("/api/recebimentos", new CreateRecebimentoDto
        {
            ContaReceberId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 100,
            DataRecebimento = DateTime.UtcNow
        });
        var contaAtualizada = await client.GetFromJsonAsync<ContaReceberResponseDto>(
            $"/api/contas-receber/{conta.Id}", JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(StatusContaReceber.Paga, contaAtualizada!.Status);
    }

    [Fact]
    public async Task RegistrarRecebimento_ValorParcial_MudaStatusParaParcial()
    {
        await using var app = new MultiClinicaFactory();
        var (pacienteId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");
        var conta = await CriarContaAsync(client, pacienteId, 100);

        await client.PostAsJsonAsync("/api/recebimentos", new CreateRecebimentoDto
        {
            ContaReceberId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 40,
            DataRecebimento = DateTime.UtcNow
        });
        var contaAtualizada = await client.GetFromJsonAsync<ContaReceberResponseDto>(
            $"/api/contas-receber/{conta.Id}", JsonOptions);

        Assert.Equal(StatusContaReceber.Parcial, contaAtualizada!.Status);
        Assert.Equal(40, contaAtualizada.ValorRecebido);
    }

    [Fact]
    public async Task EstornarRecebimento_SemMotivo_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (pacienteId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");
        var conta = await CriarContaAsync(client, pacienteId, 100);
        var recResponse = await client.PostAsJsonAsync("/api/recebimentos", new CreateRecebimentoDto
        {
            ContaReceberId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 100,
            DataRecebimento = DateTime.UtcNow
        });
        var recebimento = await recResponse.Content.ReadFromJsonAsync<RecebimentoResponseDto>();

        var response = await client.PostAsJsonAsync($"/api/recebimentos/{recebimento!.Id}/estornar",
            new EstornarRecebimentoDto { Motivo = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EstornarRecebimento_ComMotivo_RecalculaSaldoEStatus()
    {
        await using var app = new MultiClinicaFactory();
        var (pacienteId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");
        var conta = await CriarContaAsync(client, pacienteId, 100);
        var recResponse = await client.PostAsJsonAsync("/api/recebimentos", new CreateRecebimentoDto
        {
            ContaReceberId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 100,
            DataRecebimento = DateTime.UtcNow
        });
        var recebimento = await recResponse.Content.ReadFromJsonAsync<RecebimentoResponseDto>();

        var response = await client.PostAsJsonAsync($"/api/recebimentos/{recebimento!.Id}/estornar",
            new EstornarRecebimentoDto { Motivo = "Pagamento em duplicidade" });
        var contaAtualizada = await client.GetFromJsonAsync<ContaReceberResponseDto>(
            $"/api/contas-receber/{conta.Id}", JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, contaAtualizada!.ValorRecebido);
        Assert.Equal(StatusContaReceber.Aberta, contaAtualizada.Status);
    }

    [Fact]
    public async Task EstornarRecebimento_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        var (pacienteId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var adminClient = app.CreateClient();
        await LoginAsync(adminClient, "admin@a.local");
        var conta = await CriarContaAsync(adminClient, pacienteId, 100);
        var recResponse = await adminClient.PostAsJsonAsync("/api/recebimentos", new CreateRecebimentoDto
        {
            ContaReceberId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 100,
            DataRecebimento = DateTime.UtcNow
        });
        var recebimento = await recResponse.Content.ReadFromJsonAsync<RecebimentoResponseDto>();

        using var recepClient = app.CreateClient();
        await LoginAsync(recepClient, "recep@a.local");

        var response = await recepClient.PostAsJsonAsync($"/api/recebimentos/{recebimento!.Id}/estornar",
            new EstornarRecebimentoDto { Motivo = "Teste" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRecebimentosPorConta_RetornaTodosOsRecebimentosDaConta()
    {
        await using var app = new MultiClinicaFactory();
        var (pacienteId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");
        var conta = await CriarContaAsync(client, pacienteId, 100);
        await client.PostAsJsonAsync("/api/recebimentos", new CreateRecebimentoDto
        {
            ContaReceberId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 40,
            DataRecebimento = DateTime.UtcNow
        });
        await client.PostAsJsonAsync("/api/recebimentos", new CreateRecebimentoDto
        {
            ContaReceberId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 60,
            DataRecebimento = DateTime.UtcNow
        });

        var response = await client.GetAsync($"/api/contas-receber/{conta.Id}/recebimentos");
        var recebimentos = await response.Content.ReadFromJsonAsync<List<RecebimentoResponseDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, recebimentos!.Count);
        Assert.Equal(100, recebimentos.Sum(r => r.Valor));
    }

    [Fact]
    public async Task GetRecebimentosPorConta_ContaInexistente_Retorna404()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");

        var response = await client.GetAsync("/api/contas-receber/9999/recebimentos");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRecebimentosPorConta_Recepcao_Retorna200()
    {
        await using var app = new MultiClinicaFactory();
        var (pacienteId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var adminClient = app.CreateClient();
        await LoginAsync(adminClient, "admin@a.local");
        var conta = await CriarContaAsync(adminClient, pacienteId, 100);
        await adminClient.PostAsJsonAsync("/api/recebimentos", new CreateRecebimentoDto
        {
            ContaReceberId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 100,
            DataRecebimento = DateTime.UtcNow
        });

        using var recepClient = app.CreateClient();
        await LoginAsync(recepClient, "recep@a.local");

        var response = await recepClient.GetAsync($"/api/contas-receber/{conta.Id}/recebimentos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
