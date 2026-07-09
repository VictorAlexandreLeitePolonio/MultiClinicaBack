using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IAuditoriaFinanceiraRepository
{
    Task AddAsync(AuditoriaFinanceira entity);

    Task<(List<AuditoriaFinanceira> Items, int TotalCount)> GetPagedAsync(
        string? modulo,
        string? entidade,
        int? usuarioId,
        DateTime? de,
        DateTime? ate,
        int page,
        int pageSize);
}
