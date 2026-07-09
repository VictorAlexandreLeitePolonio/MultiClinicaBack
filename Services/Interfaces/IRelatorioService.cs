using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IRelatorioService
{
    Task<Result<List<RelatorioAgrupadoDto>>> GetFaturamentoAsync(DateTime de, DateTime ate, AgrupamentoRelatorio agruparPor);
    Task<Result<List<RelatorioAgrupadoDto>>> GetDespesasAsync(DateTime de, DateTime ate);
    Task<Result<ResultadoFinanceiroDto>> GetResultadoAsync(DateTime de, DateTime ate);
    Task<Result<List<ProdutoMovimentadoDto>>> GetProdutosMaisMovimentadosAsync(DateTime de, DateTime ate, int limite);
}
