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

    public async Task<Result<PagedResult<AuditoriaFinanceiraDto>>> ListarAsync(
        string? modulo, string? entidade, DateTime? dataInicio, DateTime? dataFim, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await repository.GetPagedAsync(
            usuario.ClinicaId, modulo, entidade, dataInicio, dataFim, page, pageSize);

        return Result<PagedResult<AuditoriaFinanceiraDto>>.Ok(new PagedResult<AuditoriaFinanceiraDto>
        {
            Data = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

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
}
