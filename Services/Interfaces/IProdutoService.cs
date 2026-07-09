using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IProdutoService
{
    Task<Result<PagedResult<ProdutoResponseDto>>> GetPagedAsync(
        string? nome, int? categoriaProdutoId, bool? ativo, int page, int pageSize);
    Task<Result<ProdutoResponseDto>> GetByIdAsync(int id);
    Task<Result<ProdutoResponseDto>> CreateAsync(CreateProdutoDto dto);
    Task<Result<ProdutoResponseDto>> UpdateAsync(int id, UpdateProdutoDto dto);
    Task<Result<ProdutoResponseDto>> SetActiveAsync(int id, bool active);
}
