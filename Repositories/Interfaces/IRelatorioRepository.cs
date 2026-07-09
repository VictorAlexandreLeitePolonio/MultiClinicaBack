using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IRelatorioRepository
{
    Task<List<Recebimento>> GetRecebimentosAsync(DateTime de, DateTime ate);
    Task<List<PagamentoContaPagar>> GetPagamentosAsync(DateTime de, DateTime ate);
    Task<List<MovimentacaoEstoque>> GetMovimentacoesEstoqueAsync(DateTime de, DateTime ate);
}
