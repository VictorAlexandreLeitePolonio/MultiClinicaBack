using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

public interface IContaReceberService
{
    Task<Result<PagedResult<ContaReceberResponseDto>>> GetPagedAsync(
        int? pacienteId,
        StatusContaReceber? status,
        int page,
        int pageSize);
    Task<Result<ContaReceberResponseDto>> GetByIdAsync(int id);
    Task<Result<List<ContaReceberResponseDto>>> GetInadimplentesAsync();
    Task<Result<ContaReceberResponseDto>> CreateAsync(CreateContaReceberDto dto);
    Task<Result<ContaReceberResponseDto>> UpdateAsync(int id, UpdateContaReceberDto dto);
    Task<Result<ContaReceberResponseDto>> CancelarAsync(int id, MotivoDto dto);
}
