using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IContaFinanceiraRepository
{
    Task<(List<ContaFinanceira> Items, int TotalCount)> GetPagedAsync(string? nome, int page, int pageSize);
    Task<ContaFinanceira?> GetByIdAsync(int id);
    Task<bool> ExistsActiveByNameAsync(string nome, int? excludeId);
    Task<ContaFinanceira> AddAsync(ContaFinanceira entity);
    Task SaveChangesAsync();
}
