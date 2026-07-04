using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

public interface IContaPagarService
{
    Task<Result<PagedResult<ContaPagarResponseDto>>> GetPagedAsync(int? fornecedorId, StatusContaPagar? status, int page, int pageSize);
    Task<Result<ContaPagarResponseDto>> GetByIdAsync(int id);
    Task<Result<ContaPagarResponseDto>> CreateAsync(CreateContaPagarDto dto);
    Task<Result<ContaPagarResponseDto>> UpdateAsync(int id, UpdateContaPagarDto dto);
    Task<Result<ContaPagarResponseDto>> CancelarAsync(int id);
}
