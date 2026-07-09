using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

public interface IMovimentacaoEstoqueService
{
    Task<Result<PagedResult<MovimentacaoEstoqueResponseDto>>> GetPagedAsync(
        int? produtoId,
        TipoMovimentacaoEstoque? tipo,
        int page,
        int pageSize);
    Task<Result<List<ProdutoAlertaDto>>> GetAlertasAsync();
    Task<Result<MovimentacaoEstoqueResponseDto>> RegistrarEntradaAsync(RegistrarMovimentacaoEstoqueDto dto);
    Task<Result<MovimentacaoEstoqueResponseDto>> RegistrarSaidaAsync(RegistrarMovimentacaoEstoqueDto dto, TipoMovimentacaoEstoque tipo);
    Task<Result<MovimentacaoEstoqueResponseDto>> RegistrarPerdaAsync(RegistrarMovimentacaoEstoqueDto dto);
    Task<Result<MovimentacaoEstoqueResponseDto>> AjustarAsync(AjustarEstoqueDto dto);
    Task<Result<MovimentacaoEstoqueResponseDto>> CancelarAsync(int id, MotivoDto dto);
}
