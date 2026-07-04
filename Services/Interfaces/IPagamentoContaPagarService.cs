using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IPagamentoContaPagarService
{
    Task<Result<PagamentoContaPagarResponseDto>> RegistrarAsync(CreatePagamentoContaPagarDto dto);
    Task<Result<PagamentoContaPagarResponseDto>> EstornarAsync(int id, EstornarPagamentoContaPagarDto dto);
}
