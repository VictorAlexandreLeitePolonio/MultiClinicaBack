using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IFormaPagamentoService
{
    Task<Result<PagedResult<FormaPagamentoResponseDto>>> GetPagedAsync(string? nome, int page, int pageSize);
    Task<Result<FormaPagamentoResponseDto>> GetByIdAsync(int id);
    Task<Result<FormaPagamentoResponseDto>> CreateAsync(CreateFormaPagamentoDto dto);
    Task<Result<FormaPagamentoResponseDto>> UpdateAsync(int id, UpdateFormaPagamentoDto dto);
    Task<Result<FormaPagamentoResponseDto>> SetActiveAsync(int id, bool active);
}
