using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IFornecedorService
{
    Task<Result<PagedResult<FornecedorResponseDto>>> GetPagedAsync(string? nome, int page, int pageSize);
    Task<Result<FornecedorResponseDto>> GetByIdAsync(int id);
    Task<Result<FornecedorResponseDto>> CreateAsync(CreateFornecedorDto dto);
    Task<Result<FornecedorResponseDto>> UpdateAsync(int id, UpdateFornecedorDto dto);
    Task<Result<FornecedorResponseDto>> SetActiveAsync(int id, bool active);
}
