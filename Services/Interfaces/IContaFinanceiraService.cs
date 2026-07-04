using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IContaFinanceiraService
{
    Task<Result<PagedResult<ContaFinanceiraResponseDto>>> GetPagedAsync(string? nome, int page, int pageSize);
    Task<Result<ContaFinanceiraResponseDto>> GetByIdAsync(int id);
    Task<Result<ContaFinanceiraResponseDto>> CreateAsync(CreateContaFinanceiraDto dto);
    Task<Result<ContaFinanceiraResponseDto>> UpdateAsync(int id, UpdateContaFinanceiraDto dto);
    Task<Result<ContaFinanceiraResponseDto>> SetActiveAsync(int id, bool active);
}
