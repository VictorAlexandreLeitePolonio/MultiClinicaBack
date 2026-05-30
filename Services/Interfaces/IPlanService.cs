using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Plans;
using ProjetoLP.API.Models;

namespace ProjetoLP.API.Services.Interfaces;

public interface IPlanService
{
    Task<Result<PagedResult<PlanResponseDto>>> GetPagedAsync(
        TipoPlano? tipoPlano,
        TipoSessao? tipoSessao,
        bool? isActive,
        int page,
        int pageSize);

    Task<Result<PlanResponseDto>> GetByIdAsync(int id);

    Task<Result<PlanResponseDto>> CreateAsync(CreatePlanDto dto);

    Task<Result<PlanResponseDto>> UpdateAsync(int id, UpdatePlanDto dto);

    Task<Result<bool>> ToggleStatusAsync(int id);

    Task<Result<bool>> DeleteAsync(int id);
}
