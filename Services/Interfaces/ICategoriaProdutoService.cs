using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface ICategoriaProdutoService
{
    Task<Result<PagedResult<CategoriaProdutoResponseDto>>> GetPagedAsync(string? nome, int page, int pageSize);
    Task<Result<CategoriaProdutoResponseDto>> GetByIdAsync(int id);
    Task<Result<CategoriaProdutoResponseDto>> CreateAsync(CreateCategoriaProdutoDto dto);
    Task<Result<CategoriaProdutoResponseDto>> UpdateAsync(int id, UpdateCategoriaProdutoDto dto);
    Task<Result<CategoriaProdutoResponseDto>> SetActiveAsync(int id, bool active);
}
