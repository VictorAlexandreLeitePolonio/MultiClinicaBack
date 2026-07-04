using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IFormaPagamentoRepository
{
    Task<(List<FormaPagamento> Items, int TotalCount)> GetPagedAsync(string? nome, int page, int pageSize);
    Task<FormaPagamento?> GetByIdAsync(int id);
    Task<bool> ExistsActiveByNameAsync(string nome, int? excludeId);
    Task<FormaPagamento> AddAsync(FormaPagamento entity);
    Task SaveChangesAsync();
}
