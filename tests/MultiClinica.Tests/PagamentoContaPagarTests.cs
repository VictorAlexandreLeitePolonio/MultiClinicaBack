using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PagamentoContaPagarTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<(int FornecedorId, int ContaFinanceiraId, int FormaPagamentoId)> SeedAsync(MultiClinicaFactory app)
    {
        var ids = (FornecedorId: 0, ContaFinanceiraId: 0, FormaPagamentoId: 0);
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            var fornecedor = new Fornecedor { ClinicaId = clinica.Id, Nome = "Distribuidora X" };
            db.Fornecedores.Add(fornecedor);

            var conta = new ContaFinanceira { ClinicaId = clinica.Id, Nome = "Banco", Tipo = TipoContaFinanceira.Banco };
            db.ContasFinanceiras.Add(conta);

            var forma = new FormaPagamento { ClinicaId = clinica.Id, Nome = "Transferencia" };
            db.FormasPagamento.Add(forma);

            db.Users.Add(new User
            {
                ClinicaId = clinica.Id,
                Name = "Admin",
                Email = "admin.pagamento@a.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            });
            await db.SaveChangesAsync();

            ids = (fornecedor.Id, conta.Id, forma.Id);
        });
        return ids;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ContaPagarResponseDto> CriarContaAsync(HttpClient client, int fornecedorId, decimal valor)
    {
        var response = await client.PostAsJsonAsync("/api/contas-pagar", new CreateContaPagarDto
        {
            FornecedorId = fornecedorId,
            Descricao = "Aluguel",
            ValorOriginal = valor,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });
        return (await response.Content.ReadFromJsonAsync<ContaPagarResponseDto>(JsonOptions))!;
    }

    [Fact]
    public async Task RegistrarPagamento_ValorMaiorQueSaldo_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.pagamento@a.local");
        var conta = await CriarContaAsync(client, fornecedorId, 100);

        var response = await client.PostAsJsonAsync("/api/pagamentos", new CreatePagamentoContaPagarDto
        {
            ContaPagarId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 150,
            DataPagamento = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegistrarPagamento_ValorExato_MudaStatusParaPaga()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.pagamento@a.local");
        var conta = await CriarContaAsync(client, fornecedorId, 100);

        var response = await client.PostAsJsonAsync("/api/pagamentos", new CreatePagamentoContaPagarDto
        {
            ContaPagarId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 100,
            DataPagamento = DateTime.UtcNow
        });
        var contaAtualizada = await client.GetFromJsonAsync<ContaPagarResponseDto>(
            $"/api/contas-pagar/{conta.Id}", JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(StatusContaPagar.Paga, contaAtualizada!.Status);
    }

    [Fact]
    public async Task RegistrarPagamento_ValorParcial_MudaStatusParaParcial()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.pagamento@a.local");
        var conta = await CriarContaAsync(client, fornecedorId, 100);

        await client.PostAsJsonAsync("/api/pagamentos", new CreatePagamentoContaPagarDto
        {
            ContaPagarId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 40,
            DataPagamento = DateTime.UtcNow
        });
        var contaAtualizada = await client.GetFromJsonAsync<ContaPagarResponseDto>(
            $"/api/contas-pagar/{conta.Id}", JsonOptions);

        Assert.Equal(StatusContaPagar.Parcial, contaAtualizada!.Status);
        Assert.Equal(40, contaAtualizada.ValorPago);
    }

    [Fact]
    public async Task EstornarPagamento_SemMotivo_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.pagamento@a.local");
        var conta = await CriarContaAsync(client, fornecedorId, 100);
        var pagResponse = await client.PostAsJsonAsync("/api/pagamentos", new CreatePagamentoContaPagarDto
        {
            ContaPagarId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 100,
            DataPagamento = DateTime.UtcNow
        });
        var pagamento = await pagResponse.Content.ReadFromJsonAsync<PagamentoContaPagarResponseDto>();

        var response = await client.PostAsJsonAsync($"/api/pagamentos/{pagamento!.Id}/estornar",
            new EstornarPagamentoContaPagarDto { Motivo = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EstornarPagamento_ComMotivo_RecalculaSaldoEStatus()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, contaFinanceiraId, formaId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.pagamento@a.local");
        var conta = await CriarContaAsync(client, fornecedorId, 100);
        var pagResponse = await client.PostAsJsonAsync("/api/pagamentos", new CreatePagamentoContaPagarDto
        {
            ContaPagarId = conta.Id,
            ContaFinanceiraId = contaFinanceiraId,
            FormaPagamentoId = formaId,
            Valor = 100,
            DataPagamento = DateTime.UtcNow
        });
        var pagamento = await pagResponse.Content.ReadFromJsonAsync<PagamentoContaPagarResponseDto>();

        var response = await client.PostAsJsonAsync($"/api/pagamentos/{pagamento!.Id}/estornar",
            new EstornarPagamentoContaPagarDto { Motivo = "Pagamento em duplicidade" });
        var contaAtualizada = await client.GetFromJsonAsync<ContaPagarResponseDto>(
            $"/api/contas-pagar/{conta.Id}", JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, contaAtualizada!.ValorPago);
        Assert.Equal(StatusContaPagar.Aberta, contaAtualizada.Status);
    }
}
