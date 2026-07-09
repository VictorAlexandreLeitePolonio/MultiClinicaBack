using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class MovimentacaoEstoqueTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<int> SeedAsync(MultiClinicaFactory app, int quantidadeInicial = 0)
    {
        var produtoId = 0;
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            var produto = new Produto
            {
                ClinicaId = clinica.Id,
                Nome = "Creme Hidratante",
                ValorCompra = 10,
                ValorVenda = 25,
                QuantidadeAtual = quantidadeInicial,
                QuantidadeMinima = 5
            };
            db.Produtos.Add(produto);

            db.Users.AddRange(
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Admin",
                    Email = "admin.estoque@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep",
                    Email = "recep.estoque@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Recepcao
                });
            await db.SaveChangesAsync();

            produtoId = produto.Id;
        });
        return produtoId;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RegistrarEntrada_AtualizaSaldoDoProduto()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 10);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada",
            new RegistrarMovimentacaoEstoqueDto { ProdutoId = produtoId, Quantidade = 5 });
        var movimentacao = await response.Content.ReadFromJsonAsync<MovimentacaoEstoqueResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(10, movimentacao!.QuantidadeAnterior);
        Assert.Equal(15, movimentacao.QuantidadeAtual);
    }

    [Fact]
    public async Task RegistrarSaida_MaiorQueEstoque_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 3);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/saida",
            new RegistrarMovimentacaoEstoqueDto { ProdutoId = produtoId, Quantidade = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegistrarSaida_AtualizaSaldoDoProduto()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 10);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/saida",
            new RegistrarMovimentacaoEstoqueDto { ProdutoId = produtoId, Quantidade = 4 });
        var movimentacao = await response.Content.ReadFromJsonAsync<MovimentacaoEstoqueResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(TipoMovimentacaoEstoque.Saida, movimentacao!.Tipo);
        Assert.Equal(6, movimentacao.QuantidadeAtual);
    }

    [Fact]
    public async Task RegistrarEntrada_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.estoque@a.local");

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada",
            new RegistrarMovimentacaoEstoqueDto { ProdutoId = produtoId, Quantidade = 5 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMovimentacoes_Recepcao_Retorna200()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.estoque@a.local");

        var response = await client.GetAsync("/api/estoque/movimentacoes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegistrarUsoInterno_AtualizaSaldo()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 10);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/uso-interno",
            new RegistrarMovimentacaoEstoqueDto { ProdutoId = produtoId, Quantidade = 2 });
        var movimentacao = await response.Content.ReadFromJsonAsync<MovimentacaoEstoqueResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(TipoMovimentacaoEstoque.UsoInterno, movimentacao!.Tipo);
        Assert.Equal(8, movimentacao.QuantidadeAtual);
    }

    [Fact]
    public async Task RegistrarPerda_SemObservacao_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 10);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/perda",
            new RegistrarMovimentacaoEstoqueDto { ProdutoId = produtoId, Quantidade = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ajustar_ComObservacao_RegistraDiferenca()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 10);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/ajuste",
            new AjustarEstoqueDto { ProdutoId = produtoId, NovaQuantidade = 14, Observacao = "Contagem física" });
        var movimentacao = await response.Content.ReadFromJsonAsync<MovimentacaoEstoqueResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(TipoMovimentacaoEstoque.Ajuste, movimentacao!.Tipo);
        Assert.Equal(4, movimentacao.Quantidade);
        Assert.Equal(14, movimentacao.QuantidadeAtual);
    }

    [Fact]
    public async Task CancelarEntrada_ReverteSaldo()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 10);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada",
            new RegistrarMovimentacaoEstoqueDto { ProdutoId = produtoId, Quantidade = 5 });
        var criada = await createResponse.Content.ReadFromJsonAsync<MovimentacaoEstoqueResponseDto>(JsonOptions);

        var response = await client.PostAsJsonAsync($"/api/estoque/movimentacoes/{criada!.Id}/cancelar",
            new MotivoDto { Motivo = "Lançamento errado" });
        var cancelada = await response.Content.ReadFromJsonAsync<MovimentacaoEstoqueResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(cancelada!.IsCancelada);

        var produtoResponse = await client.GetAsync($"/api/produtos/{produtoId}");
        var produto = await produtoResponse.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);
        Assert.Equal(10, produto!.QuantidadeAtual);
    }

    [Fact]
    public async Task CancelarEntrada_ConsumidaPorSaida_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 0);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");
        var entradaResponse = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada",
            new RegistrarMovimentacaoEstoqueDto { ProdutoId = produtoId, Quantidade = 5 });
        var entrada = await entradaResponse.Content.ReadFromJsonAsync<MovimentacaoEstoqueResponseDto>(JsonOptions);
        await client.PostAsJsonAsync("/api/estoque/movimentacoes/saida",
            new RegistrarMovimentacaoEstoqueDto { ProdutoId = produtoId, Quantidade = 4 });

        var response = await client.PostAsJsonAsync($"/api/estoque/movimentacoes/{entrada!.Id}/cancelar",
            new MotivoDto { Motivo = "Lançamento errado" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAlertas_ProdutoAbaixoDoMinimo_Aparece()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 2);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");

        var response = await client.GetAsync("/api/estoque/alertas");
        var alertas = await response.Content.ReadFromJsonAsync<List<ProdutoAlertaDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(alertas!, a => a.ProdutoId == produtoId);
    }

    [Fact]
    public async Task GetAlertas_ProdutoAcimaDoMinimo_NaoAparece()
    {
        await using var app = new MultiClinicaFactory();
        var produtoId = await SeedAsync(app, quantidadeInicial: 100);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.estoque@a.local");

        var response = await client.GetAsync("/api/estoque/alertas");
        var alertas = await response.Content.ReadFromJsonAsync<List<ProdutoAlertaDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(alertas!, a => a.ProdutoId == produtoId);
    }
}
