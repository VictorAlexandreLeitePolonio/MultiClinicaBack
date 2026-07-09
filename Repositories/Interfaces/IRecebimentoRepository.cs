using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IRecebimentoRepository
{
    Task<Recebimento?> GetByIdAsync(int id);
    Task<List<Recebimento>> GetByContaReceberAsync(int contaReceberId);
    Task<bool> ContaFinanceiraExistsAsync(int id);
    Task<bool> FormaPagamentoExistsAsync(int id);
    Task AddMovimentacaoAsync(MovimentacaoFinanceira movimentacao);
    Task SaveChangesAsync();
}
