using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IAuditoriaFinanceiraService
{
    Task RegistrarAsync(
        string modulo,
        string acao,
        string entidade,
        int entidadeId,
        object? dadosAntes,
        object? dadosDepois,
        string? motivo = null);

    Task<Result<PagedResult<AuditoriaFinanceiraResponseDto>>> GetPagedAsync(
        string? modulo,
        string? entidade,
        int? usuarioId,
        DateTime? de,
        DateTime? ate,
        int page,
        int pageSize);
}
