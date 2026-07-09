using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface ICompraRepository
{
    Task<(List<Compra> Items, int TotalCount)> GetPagedAsync(int? fornecedorId, StatusCompra? status, int page, int pageSize);
    Task<Compra?> GetByIdAsync(int id);
    Task<bool> FornecedorExistsAsync(int fornecedorId);
    Task<Compra> AddAsync(Compra entity);
    Task SaveChangesAsync();
}
