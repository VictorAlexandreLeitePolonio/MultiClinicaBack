using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class RelatorioService(IRelatorioRepository repository) : IRelatorioService
{
    public async Task<Result<List<RelatorioAgrupadoDto>>> GetFaturamentoAsync(
        DateTime de,
        DateTime ate,
        AgrupamentoRelatorio agruparPor)
    {
        var periodoInvalido = ValidarPeriodo<List<RelatorioAgrupadoDto>>(de, ate);
        if (periodoInvalido is not null)
            return periodoInvalido;

        var recebimentos = await repository.GetRecebimentosAsync(de, ate);
        IEnumerable<IGrouping<string, Recebimento>> agrupado = agruparPor switch
        {
            AgrupamentoRelatorio.FormaPagamento => recebimentos.GroupBy(r => r.FormaPagamento.Nome),
            AgrupamentoRelatorio.Categoria => recebimentos.GroupBy(r => r.ContaReceber.CategoriaFinanceira?.Nome ?? "Sem categoria"),
            _ => recebimentos.GroupBy(r => r.DataRecebimento.ToString("yyyy-MM"))
        };

        return Result<List<RelatorioAgrupadoDto>>.Ok(agrupado
            .Select(g => new RelatorioAgrupadoDto { Chave = g.Key, Valor = g.Sum(r => r.Valor) })
            .OrderBy(r => r.Chave)
            .ToList());
    }

    public async Task<Result<List<RelatorioAgrupadoDto>>> GetDespesasAsync(DateTime de, DateTime ate)
    {
        var periodoInvalido = ValidarPeriodo<List<RelatorioAgrupadoDto>>(de, ate);
        if (periodoInvalido is not null)
            return periodoInvalido;

        var pagamentos = await repository.GetPagamentosAsync(de, ate);
        return Result<List<RelatorioAgrupadoDto>>.Ok(pagamentos
            .GroupBy(p => p.ContaPagar.CategoriaFinanceira?.Nome ?? "Sem categoria")
            .Select(g => new RelatorioAgrupadoDto { Chave = g.Key, Valor = g.Sum(p => p.Valor) })
            .OrderBy(r => r.Chave)
            .ToList());
    }

    public async Task<Result<ResultadoFinanceiroDto>> GetResultadoAsync(DateTime de, DateTime ate)
    {
        var periodoInvalido = ValidarPeriodo<ResultadoFinanceiroDto>(de, ate);
        if (periodoInvalido is not null)
            return periodoInvalido;

        var faturamento = (await repository.GetRecebimentosAsync(de, ate)).Sum(r => r.Valor);
        var despesas = (await repository.GetPagamentosAsync(de, ate)).Sum(p => p.Valor);

        return Result<ResultadoFinanceiroDto>.Ok(new ResultadoFinanceiroDto
        {
            Faturamento = faturamento,
            Despesas = despesas,
            Resultado = faturamento - despesas
        });
    }

    public async Task<Result<List<ProdutoMovimentadoDto>>> GetProdutosMaisMovimentadosAsync(
        DateTime de,
        DateTime ate,
        int limite)
    {
        var periodoInvalido = ValidarPeriodo<List<ProdutoMovimentadoDto>>(de, ate);
        if (periodoInvalido is not null)
            return periodoInvalido;
        if (limite <= 0)
            return Result<List<ProdutoMovimentadoDto>>.Ok([]);

        var movimentacoes = await repository.GetMovimentacoesEstoqueAsync(de, ate);
        return Result<List<ProdutoMovimentadoDto>>.Ok(movimentacoes
            .GroupBy(m => new { m.ProdutoId, m.Produto.Nome })
            .Select(g => new ProdutoMovimentadoDto
            {
                ProdutoId = g.Key.ProdutoId,
                Nome = g.Key.Nome,
                QuantidadeMovimentada = g.Sum(m => m.Quantidade)
            })
            .OrderByDescending(p => p.QuantidadeMovimentada)
            .ThenBy(p => p.Nome)
            .Take(limite)
            .ToList());
    }

    private static Result<T>? ValidarPeriodo<T>(DateTime de, DateTime ate) =>
        de > ate
            ? Result<T>.Fail(ErrorCodes.InvalidDate, "A data inicial não pode ser maior que a data final.")
            : null;
}
