using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

public interface ICompraService
{
    Task<Result<PagedResult<CompraResponseDto>>> GetPagedAsync(int? fornecedorId, StatusCompra? status, int page, int pageSize);
    Task<Result<CompraResponseDto>> GetByIdAsync(int id);
    Task<Result<CompraResponseDto>> CreateAsync(CreateCompraDto dto);
    Task<Result<CompraResponseDto>> UpdateAsync(int id, UpdateCompraDto dto);
    Task<Result<CompraResponseDto>> AprovarAsync(int id);
    Task<Result<CompraResponseDto>> ReceberAsync(int id);
    Task<Result<CompraResponseDto>> GerarContaPagarAsync(int id, GerarContaPagarDto dto);
    Task<Result<CompraResponseDto>> CancelarAsync(int id, MotivoDto dto);
}
