using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IPagamentoContaPagarRepository
{
    Task<PagamentoContaPagar?> GetByIdAsync(int id);
    Task<bool> ContaFinanceiraExistsAsync(int id);
    Task<bool> FormaPagamentoExistsAsync(int id);
    Task AddMovimentacaoAsync(MovimentacaoFinanceira movimentacao);
    Task SaveChangesAsync();
}
