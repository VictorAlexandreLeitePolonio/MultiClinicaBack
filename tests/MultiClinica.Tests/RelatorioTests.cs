using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class RelatorioTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record SeedIds(
        int ClinicaId,
        int PacienteId,
        int FornecedorId,
        int ContaFinanceiraId,
        int FormaPagamentoId,
        int CategoriaReceitaId,
        int CategoriaDespesaId,
        int ContaReceberId,
        int ContaPagarId,
        int ProdutoId);

    private static async Task<SeedIds> SeedAsync(MultiClinicaFactory app)
    {
        SeedIds ids = null!;
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            var paciente = new Patient { ClinicaId = clinica.Id, Name = "Paciente Relatorio" };
            var fornecedor = new Fornecedor { ClinicaId = clinica.Id, Nome = "Fornecedor Relatorio" };
            var contaFinanceira = new ContaFinanceira { ClinicaId = clinica.Id, Nome = "Caixa", Tipo = TipoContaFinanceira.Caixa };
            var formaPagamento = new FormaPagamento { ClinicaId = clinica.Id, Nome = "Pix" };
            var categoriaReceita = new CategoriaFinanceira { ClinicaId = clinica.Id, Nome = "Consultas", Tipo = TipoCategoriaFinanceira.Receita };
            var categoriaDespesa = new CategoriaFinanceira { ClinicaId = clinica.Id, Nome = "Aluguel", Tipo = TipoCategoriaFinanceira.Despesa };
            var produto = new Produto
            {
                ClinicaId = clinica.Id,
                Nome = "Creme Hidratante",
                ValorCompra = 10,
                ValorVenda = 25,
                QuantidadeAtual = 100,
                QuantidadeMinima = 5
            };

            db.Patients.Add(paciente);
            db.Fornecedores.Add(fornecedor);
            db.ContasFinanceiras.Add(contaFinanceira);
            db.FormasPagamento.Add(formaPagamento);
            db.CategoriasFinanceiras.AddRange(categoriaReceita, categoriaDespesa);
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();

            var contaReceber = new ContaReceber
            {
                ClinicaId = clinica.Id,
                PacienteId = paciente.Id,
                CategoriaFinanceiraId = categoriaReceita.Id,
                Descricao = "Consulta",
                ValorOriginal = 500,
                ValorTotal = 500,
                DataEmissao = DateTime.UtcNow,
                DataVencimento = DateTime.UtcNow.AddDays(5)
            };
            var contaPagar = new ContaPagar
            {
                ClinicaId = clinica.Id,
                FornecedorId = fornecedor.Id,
                CategoriaFinanceiraId = categoriaDespesa.Id,
                Descricao = "Aluguel",
                ValorOriginal = 300,
                ValorTotal = 300,
                DataEmissao = DateTime.UtcNow,
                DataVencimento = DateTime.UtcNow.AddDays(5)
            };
            db.ContasReceber.Add(contaReceber);
            db.ContasPagar.Add(contaPagar);
            db.Users.AddRange(
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Admin Relatorio",
                    Email = "admin.relatorio@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep Relatorio",
                    Email = "recep.relatorio@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Recepcao
                });
            await db.SaveChangesAsync();

            ids = new SeedIds(
                clinica.Id,
                paciente.Id,
                fornecedor.Id,
                contaFinanceira.Id,
                formaPagamento.Id,
                categoriaReceita.Id,
                categoriaDespesa.Id,
                contaReceber.Id,
                contaPagar.Id,
                produto.Id);
        });
        return ids;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    private static async Task AdicionarRecebimentoAsync(
        MultiClinicaFactory app,
        SeedIds ids,
        decimal valor,
        DateTime data,
        bool estornado = false) =>
        await app.SeedAsync(async db =>
        {
            db.Recebimentos.Add(new Recebimento
            {
                ClinicaId = ids.ClinicaId,
                ContaReceberId = ids.ContaReceberId,
                ContaFinanceiraId = ids.ContaFinanceiraId,
                FormaPagamentoId = ids.FormaPagamentoId,
                Valor = valor,
                DataRecebimento = data,
                IsEstornado = estornado
            });
            await db.SaveChangesAsync();
        });

    private static async Task AdicionarPagamentoAsync(
        MultiClinicaFactory app,
        SeedIds ids,
        decimal valor,
        DateTime data,
        bool estornado = false) =>
        await app.SeedAsync(async db =>
        {
            db.PagamentosContaPagar.Add(new PagamentoContaPagar
            {
                ClinicaId = ids.ClinicaId,
                ContaPagarId = ids.ContaPagarId,
                ContaFinanceiraId = ids.ContaFinanceiraId,
                FormaPagamentoId = ids.FormaPagamentoId,
                Valor = valor,
                DataPagamento = data,
                IsEstornado = estornado
            });
            await db.SaveChangesAsync();
        });

    private static async Task AdicionarMovimentacaoEstoqueAsync(
        MultiClinicaFactory app,
        SeedIds ids,
        int quantidade,
        DateTime data,
        bool cancelada = false) =>
        await app.SeedAsync(async db =>
        {
            db.MovimentacoesEstoque.Add(new MovimentacaoEstoque
            {
                ClinicaId = ids.ClinicaId,
                ProdutoId = ids.ProdutoId,
                Tipo = TipoMovimentacaoEstoque.Entrada,
                Quantidade = quantidade,
                QuantidadeAnterior = 0,
                QuantidadeAtual = quantidade,
                UsuarioId = 0,
                IsCancelada = cancelada,
                CreatedAt = data
            });
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task Faturamento_AgrupadoPorPeriodo_SomaRecebimentosNaoEstornados()
    {
        await using var app = new MultiClinicaFactory();
        var ids = await SeedAsync(app);
        await AdicionarRecebimentoAsync(app, ids, 100, new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        await AdicionarRecebimentoAsync(app, ids, 50, new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc), estornado: true);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.relatorio@a.local");

        var response = await client.GetAsync("/api/relatorios/faturamento?de=2026-07-01&ate=2026-07-31&agruparPor=Periodo");
        var relatorio = await response.Content.ReadFromJsonAsync<List<RelatorioAgrupadoDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100, relatorio!.Single(r => r.Chave == "2026-07").Valor);
    }

    [Fact]
    public async Task Faturamento_DataInicialMaiorQueFinal_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.relatorio@a.local");

        var response = await client.GetAsync("/api/relatorios/faturamento?de=2026-07-31&ate=2026-07-01&agruparPor=Periodo");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Despesas_AgrupadasPorCategoria_SomaPagamentosNaoEstornados()
    {
        await using var app = new MultiClinicaFactory();
        var ids = await SeedAsync(app);
        await AdicionarPagamentoAsync(app, ids, 300, new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        await AdicionarPagamentoAsync(app, ids, 75, new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc), estornado: true);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.relatorio@a.local");

        var response = await client.GetAsync("/api/relatorios/despesas?de=2026-07-01&ate=2026-07-31");
        var relatorio = await response.Content.ReadFromJsonAsync<List<RelatorioAgrupadoDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(300, relatorio!.Single(r => r.Chave == "Aluguel").Valor);
    }

    [Fact]
    public async Task Resultado_CalculaFaturamentoMenosDespesas()
    {
        await using var app = new MultiClinicaFactory();
        var ids = await SeedAsync(app);
        await AdicionarRecebimentoAsync(app, ids, 1000, new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        await AdicionarPagamentoAsync(app, ids, 400, new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.relatorio@a.local");

        var response = await client.GetAsync("/api/relatorios/resultado?de=2026-07-01&ate=2026-07-31");
        var resultado = await response.Content.ReadFromJsonAsync<ResultadoFinanceiroDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1000, resultado!.Faturamento);
        Assert.Equal(400, resultado.Despesas);
        Assert.Equal(600, resultado.Resultado);
    }

    [Fact]
    public async Task ProdutosMaisMovimentados_SomaMovimentacoesNaoCanceladas()
    {
        await using var app = new MultiClinicaFactory();
        var ids = await SeedAsync(app);
        await AdicionarMovimentacaoEstoqueAsync(app, ids, 30, new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        await AdicionarMovimentacaoEstoqueAsync(app, ids, 20, new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc));
        await AdicionarMovimentacaoEstoqueAsync(app, ids, 999, new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc), cancelada: true);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.relatorio@a.local");

        var response = await client.GetAsync("/api/relatorios/produtos-mais-movimentados?de=2026-07-01&ate=2026-07-31");
        var relatorio = await response.Content.ReadFromJsonAsync<List<ProdutoMovimentadoDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50, relatorio!.Single(p => p.ProdutoId == ids.ProdutoId).QuantidadeMovimentada);
    }

    [Fact]
    public async Task Relatorio_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.relatorio@a.local");

        var response = await client.GetAsync("/api/relatorios/resultado?de=2026-07-01&ate=2026-07-31");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
