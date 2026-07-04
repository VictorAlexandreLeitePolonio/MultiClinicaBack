using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface ICategoriaFinanceiraRepository
{
    Task<(List<CategoriaFinanceira> Items, int TotalCount)> GetPagedAsync(
        string? nome,
        TipoCategoriaFinanceira? tipo,
        int page,
        int pageSize);
    Task<CategoriaFinanceira?> GetByIdAsync(int id);
    Task<bool> ExistsActiveByNameAndTipoAsync(string nome, TipoCategoriaFinanceira tipo, int? excludeId);
    Task<CategoriaFinanceira> AddAsync(CategoriaFinanceira entity);
    Task SaveChangesAsync();
}
