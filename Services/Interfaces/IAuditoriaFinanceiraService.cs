using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IAuditoriaFinanceiraService
{
    Task<Result<PagedResult<AuditoriaFinanceiraDto>>> ListarAsync(
        string? modulo, string? entidade, DateTime? dataInicio, DateTime? dataFim, int page, int pageSize);

    Task RegistrarAsync(
        string modulo,
        string acao,
        string entidade,
        int entidadeId,
        object? dadosAntes,
        object? dadosDepois,
        string? motivo = null);
}
