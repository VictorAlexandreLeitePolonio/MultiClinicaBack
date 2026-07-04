using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

public interface ICategoriaFinanceiraService
{
    Task<Result<PagedResult<CategoriaFinanceiraResponseDto>>> GetPagedAsync(
        string? nome,
        TipoCategoriaFinanceira? tipo,
        int page,
        int pageSize);
    Task<Result<CategoriaFinanceiraResponseDto>> GetByIdAsync(int id);
    Task<Result<CategoriaFinanceiraResponseDto>> CreateAsync(CreateCategoriaFinanceiraDto dto);
    Task<Result<CategoriaFinanceiraResponseDto>> UpdateAsync(int id, UpdateCategoriaFinanceiraDto dto);
    Task<Result<CategoriaFinanceiraResponseDto>> SetActiveAsync(int id, bool active);
}
