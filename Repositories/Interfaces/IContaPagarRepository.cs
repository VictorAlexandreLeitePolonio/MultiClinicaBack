using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IContaPagarRepository
{
    Task<(List<ContaPagar> Items, int TotalCount)> GetPagedAsync(int? fornecedorId, StatusContaPagar? status, int page, int pageSize);
    Task<ContaPagar?> GetByIdAsync(int id);
    Task<bool> FornecedorExistsAsync(int fornecedorId);
    Task<bool> CategoriaExistsAsync(int categoriaId);
    Task<ContaPagar> AddAsync(ContaPagar entity);
    Task SaveChangesAsync();
}
