using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class CompraTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<(int FornecedorId, int ProdutoId)> SeedAsync(MultiClinicaFactory app)
    {
        var ids = (FornecedorId: 0, ProdutoId: 0);
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            var fornecedor = new Fornecedor { ClinicaId = clinica.Id, Nome = "Distribuidora X" };
            var produto = new Produto
            {
                ClinicaId = clinica.Id,
                Nome = "Creme Hidratante",
                ValorCompra = 10,
                ValorVenda = 25,
                QuantidadeAtual = 0,
                QuantidadeMinima = 5
            };
            db.Fornecedores.Add(fornecedor);
            db.Produtos.Add(produto);

            db.Users.AddRange(
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Admin",
                    Email = "admin.compra@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep",
                    Email = "recep.compra@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Recepcao
                });
            await db.SaveChangesAsync();

            ids = (fornecedor.Id, produto.Id);
        });
        return ids;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    private static CreateCompraDto ValidDto(int fornecedorId, int produtoId) => new()
    {
        FornecedorId = fornecedorId,
        DataCompra = DateTime.UtcNow,
        Itens = [new CompraItemDto { ProdutoId = produtoId, Quantidade = 10, ValorUnitario = 8 }]
    };

    [Fact]
    public async Task CriarCompra_AdminComItemValido_Retorna201ComTotalCalculado()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");

        var response = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));
        var compra = await response.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(80, compra!.ValorTotal);
        Assert.Equal(StatusCompra.Aberta, compra.Status);
    }

    [Fact]
    public async Task CriarCompra_SemItens_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, _) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");

        var response = await client.PostAsJsonAsync("/api/compras",
            new CreateCompraDto { FornecedorId = fornecedorId, DataCompra = DateTime.UtcNow });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarCompra_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.compra@a.local");

        var response = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EditarCompra_Aberta_RecalculaTotal()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));
        var criada = await createResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);

        var response = await client.PutAsJsonAsync($"/api/compras/{criada!.Id}", new UpdateCompraDto
        {
            FornecedorId = fornecedorId,
            DataCompra = DateTime.UtcNow,
            Itens = [new CompraItemDto { ProdutoId = produtoId, Quantidade = 5, ValorUnitario = 8 }]
        });
        var editada = await response.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(40, editada!.ValorTotal);
    }

    [Fact]
    public async Task AprovarReceberEGerarContaPagar_FluxoValido_AtualizaEstoqueEFinanceiro()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));
        var criada = await createResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);

        var aprovarResponse = await client.PostAsync($"/api/compras/{criada!.Id}/aprovar", null);
        var aprovada = await aprovarResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);
        var receberResponse = await client.PostAsync($"/api/compras/{criada.Id}/receber", null);
        var recebida = await receberResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);
        var contaResponse = await client.PostAsJsonAsync($"/api/compras/{criada.Id}/gerar-conta-pagar",
            new GerarContaPagarDto { DataVencimento = DateTime.UtcNow.AddDays(30) });
        var compraComConta = await contaResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, aprovarResponse.StatusCode);
        Assert.Equal(StatusCompra.Aprovada, aprovada!.Status);
        Assert.Equal(HttpStatusCode.OK, receberResponse.StatusCode);
        Assert.Equal(StatusCompra.Recebida, recebida!.Status);
        Assert.Equal(HttpStatusCode.OK, contaResponse.StatusCode);
        Assert.True(compraComConta!.GerouContaPagar);
        Assert.NotNull(compraComConta.ContaPagarId);

        var produtoResponse = await client.GetAsync($"/api/produtos/{produtoId}");
        var produto = await produtoResponse.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);
        Assert.Equal(10, produto!.QuantidadeAtual);

        var movimentacoesResponse = await client.GetAsync($"/api/estoque/movimentacoes?produtoId={produtoId}");
        var movimentacoes = await movimentacoesResponse.Content
            .ReadFromJsonAsync<PagedResult<MovimentacaoEstoqueResponseDto>>(JsonOptions);
        Assert.Contains(movimentacoes!.Data, m => m.Tipo == TipoMovimentacaoEstoque.Compra && m.OrigemId == criada.Id);

        var contaPagarResponse = await client.GetAsync($"/api/contas-pagar/{compraComConta.ContaPagarId}");
        var contaPagar = await contaPagarResponse.Content.ReadFromJsonAsync<ContaPagarResponseDto>(JsonOptions);
        Assert.Equal(80, contaPagar!.ValorTotal);
        Assert.Equal(OrigemContaPagar.Compra, contaPagar.Origem);
        Assert.Equal(criada.Id, contaPagar.OrigemId);
    }

    [Fact]
    public async Task GerarContaPagar_JaGerada_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));
        var criada = await createResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);
        await client.PostAsJsonAsync($"/api/compras/{criada!.Id}/gerar-conta-pagar",
            new GerarContaPagarDto { DataVencimento = DateTime.UtcNow.AddDays(30) });

        var response = await client.PostAsJsonAsync($"/api/compras/{criada.Id}/gerar-conta-pagar",
            new GerarContaPagarDto { DataVencimento = DateTime.UtcNow.AddDays(30) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelarMovimentacaoGeradaPorCompra_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));
        var criada = await createResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);
        await client.PostAsync($"/api/compras/{criada!.Id}/aprovar", null);
        await client.PostAsync($"/api/compras/{criada.Id}/receber", null);
        var movimentacoesResponse = await client.GetAsync($"/api/estoque/movimentacoes?produtoId={produtoId}");
        var movimentacoes = await movimentacoesResponse.Content
            .ReadFromJsonAsync<PagedResult<MovimentacaoEstoqueResponseDto>>(JsonOptions);
        var movimentacaoId = movimentacoes!.Data.First(m => m.Tipo == TipoMovimentacaoEstoque.Compra).Id;

        var response = await client.PostAsJsonAsync($"/api/estoque/movimentacoes/{movimentacaoId}/cancelar",
            new MotivoDto { Motivo = "Tentativa" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReceberCompra_Aberta_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));
        var criada = await createResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);

        var response = await client.PostAsync($"/api/compras/{criada!.Id}/receber", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelarCompra_Recebida_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));
        var criada = await createResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);
        await client.PostAsync($"/api/compras/{criada!.Id}/aprovar", null);
        await client.PostAsync($"/api/compras/{criada.Id}/receber", null);

        var response = await client.PostAsJsonAsync($"/api/compras/{criada.Id}/cancelar",
            new MotivoDto { Motivo = "Tentativa" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelarCompra_Aberta_MudaParaCancelada()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));
        var criada = await createResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);

        var response = await client.PostAsJsonAsync($"/api/compras/{criada!.Id}/cancelar",
            new MotivoDto { Motivo = "Fornecedor cancelou" });
        var cancelada = await response.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StatusCompra.Cancelada, cancelada!.Status);
    }

    [Fact]
    public async Task EditarCompra_Aprovada_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (fornecedorId, produtoId) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.compra@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/compras", ValidDto(fornecedorId, produtoId));
        var criada = await createResponse.Content.ReadFromJsonAsync<CompraResponseDto>(JsonOptions);
        await app.SeedAsync(async db =>
        {
            var entity = await db.Compras.FirstAsync(c => c.Id == criada!.Id);
            entity.Status = StatusCompra.Aprovada;
            await db.SaveChangesAsync();
        });

        var response = await client.PutAsJsonAsync($"/api/compras/{criada!.Id}", ValidDto(fornecedorId, produtoId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
