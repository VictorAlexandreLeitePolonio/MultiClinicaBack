using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

public interface ICaixaService
{
    Task<Result<PagedResult<CaixaResponseDto>>> GetPagedAsync(StatusCaixa? status, int page, int pageSize);
    Task<Result<CaixaResponseDto>> GetAtualAsync();
    Task<Result<CaixaResponseDto>> GetByIdAsync(int id);
    Task<Result<List<MovimentacaoResumoDto>>> GetMovimentacoesAsync(int id);
    Task<Result<CaixaResponseDto>> AbrirAsync(AbrirCaixaDto dto);
    Task<Result<CaixaResponseDto>> FecharAsync(int id, FecharCaixaDto dto);
    Task<Result<CaixaResponseDto>> ReabrirAsync(int id, MotivoDto dto);
    Task<Result<CaixaResponseDto>> AjustarAsync(int id, AjustarCaixaDto dto);
    Task<Result<CaixaResponseDto>> CancelarAsync(int id, MotivoDto dto);
}

public class MovimentacaoResumoDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Origem { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime DataMovimentacao { get; set; }
}
