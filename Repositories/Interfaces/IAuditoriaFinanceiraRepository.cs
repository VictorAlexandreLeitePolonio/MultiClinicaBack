using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IAuditoriaFinanceiraRepository
{
    Task AddAsync(AuditoriaFinanceira entity);

    // ponytail: projeta direto p/ DTO com join em Users — evita N+1 e um 2º repositório só p/ nomes.
    Task<(List<AuditoriaFinanceiraDto> Items, int TotalCount)> GetPagedAsync(
        int clinicaId, string? modulo, string? entidade,
        DateTime? inicio, DateTime? fim, int page, int pageSize);
}
