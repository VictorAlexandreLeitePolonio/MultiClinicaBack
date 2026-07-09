using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IRecebimentoService
{
    Task<Result<RecebimentoResponseDto>> RegistrarAsync(CreateRecebimentoDto dto);
    Task<Result<List<RecebimentoResponseDto>>> GetByContaReceberAsync(int contaReceberId);
    Task<Result<RecebimentoResponseDto>> EstornarAsync(int id, EstornarRecebimentoDto dto);
}
