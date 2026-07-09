using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface ICategoriaProdutoRepository
{
    Task<(List<CategoriaProduto> Items, int TotalCount)> GetPagedAsync(string? nome, int page, int pageSize);
    Task<CategoriaProduto?> GetByIdAsync(int id);
    Task<bool> ExistsActiveByNameAsync(string nome, int? excludeId);
    Task<bool> ExistsActiveAsync(int id);
    Task<CategoriaProduto> AddAsync(CategoriaProduto entity);
    Task SaveChangesAsync();
}
