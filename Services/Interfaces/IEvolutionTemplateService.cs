using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Evolution;

namespace MultiClinica.API.Services.Interfaces;

public interface IEvolutionTemplateService
{
    Task<Result<PagedResult<EvolutionTemplateResponseDto>>> GetPagedAsync(int page, int pageSize);
    Task<Result<EvolutionTemplateResponseDto>> GetByIdAsync(int id);
    Task<Result<EvolutionTemplateResponseDto>> CreateAsync(CreateEvolutionTemplateDto dto);
    Task<Result<bool>> UpdateAsync(int id, UpdateEvolutionTemplateDto dto);
    Task<Result<bool>> DeactivateAsync(int id);
}
