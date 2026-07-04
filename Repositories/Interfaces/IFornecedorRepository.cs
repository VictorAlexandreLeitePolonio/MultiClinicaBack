using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IFornecedorRepository
{
    Task<(List<Fornecedor> Items, int TotalCount)> GetPagedAsync(string? nome, int page, int pageSize);
    Task<Fornecedor?> GetByIdAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<Fornecedor> AddAsync(Fornecedor entity);
}
