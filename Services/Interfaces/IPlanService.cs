using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Plans;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

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
