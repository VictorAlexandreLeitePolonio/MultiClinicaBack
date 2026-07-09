using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IProdutoRepository
{
    Task<(List<Produto> Items, int TotalCount)> GetPagedAsync(string? nome, int? categoriaProdutoId, bool? ativo, int page, int pageSize);
    Task<Produto?> GetByIdAsync(int id);
    Task<bool> CategoriaExistsAsync(int categoriaProdutoId);
    Task<Produto> AddAsync(Produto entity);
    Task SaveChangesAsync();
}
