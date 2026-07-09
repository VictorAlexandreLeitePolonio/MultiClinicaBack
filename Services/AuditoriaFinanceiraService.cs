using System.Text.Json;
using System.Text.Json.Serialization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class AuditoriaFinanceiraService(
    IAuditoriaFinanceiraRepository repository,
    IUsuarioLogadoService usuario,
    IHttpContextAccessor httpContextAccessor) : IAuditoriaFinanceiraService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task RegistrarAsync(
        string modulo,
        string acao,
        string entidade,
        int entidadeId,
        object? dadosAntes,
        object? dadosDepois,
        string? motivo = null)
    {
        var http = httpContextAccessor.HttpContext;

        await repository.AddAsync(new AuditoriaFinanceira
        {
            ClinicaId = usuario.ClinicaId,
            UsuarioId = usuario.UserId,
            Modulo = modulo,
            Acao = acao,
            Entidade = entidade,
            EntidadeId = entidadeId,
            DadosAntes = dadosAntes is null ? null : JsonSerializer.Serialize(dadosAntes, JsonOptions),
            DadosDepois = dadosDepois is null ? null : JsonSerializer.Serialize(dadosDepois, JsonOptions),
            Motivo = motivo,
            DataAcao = DateTime.UtcNow,
            Ip = http?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http?.Request.Headers.UserAgent.ToString()
        });
    }

    public async Task<Result<PagedResult<AuditoriaFinanceiraResponseDto>>> GetPagedAsync(
        string? modulo,
        string? entidade,
        int? usuarioId,
        DateTime? de,
        DateTime? ate,
        int page,
        int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(modulo, entidade, usuarioId, de, ate, page, pageSize);
        return Result<PagedResult<AuditoriaFinanceiraResponseDto>>.Ok(new PagedResult<AuditoriaFinanceiraResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    private static AuditoriaFinanceiraResponseDto Map(AuditoriaFinanceira a) => new()
    {
        Id = a.Id,
        UsuarioId = a.UsuarioId,
        Modulo = a.Modulo,
        Acao = a.Acao,
        Entidade = a.Entidade,
        EntidadeId = a.EntidadeId,
        DadosAntes = a.DadosAntes,
        DadosDepois = a.DadosDepois,
        Motivo = a.Motivo,
        DataAcao = a.DataAcao,
        Ip = a.Ip,
        UserAgent = a.UserAgent
    };
}
