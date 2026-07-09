using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Services;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/estoque")]
public class EstoqueController(MovimentacaoEstoqueService service) : ControllerBase
{
    [HttpGet("movimentacoes")]
    [RequirePermission(Permissions.Estoque.MovimentacoesVisualizar)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? produtoId,
        [FromQuery] TipoMovimentacaoEstoque? tipo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        => Ok((await service.GetPagedAsync(produtoId, tipo, page, pageSize)).Value);

    [HttpGet("alertas")]
    [RequirePermission(Permissions.Estoque.AlertasVisualizar)]
    public async Task<IActionResult> GetAlertas()
        => Ok((await service.GetAlertasAsync()).Value);

    [HttpPost("movimentacoes/entrada")]
    [RequirePermission(Permissions.Estoque.Entrada)]
    public async Task<IActionResult> Entrada(RegistrarMovimentacaoEstoqueDto dto)
    {
        var result = await service.RegistrarEntradaAsync(dto);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("movimentacoes/saida")]
    [RequirePermission(Permissions.Estoque.Saida)]
    public async Task<IActionResult> Saida(RegistrarMovimentacaoEstoqueDto dto)
    {
        var result = await service.RegistrarSaidaAsync(dto, TipoMovimentacaoEstoque.Saida);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("movimentacoes/uso-interno")]
    [RequirePermission(Permissions.Estoque.Saida)]
    public async Task<IActionResult> UsoInterno(RegistrarMovimentacaoEstoqueDto dto)
    {
        var result = await service.RegistrarSaidaAsync(dto, TipoMovimentacaoEstoque.UsoInterno);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("movimentacoes/perda")]
    [RequirePermission(Permissions.Estoque.Perda)]
    public async Task<IActionResult> Perda(RegistrarMovimentacaoEstoqueDto dto)
    {
        var result = await service.RegistrarPerdaAsync(dto);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("movimentacoes/ajuste")]
    [RequirePermission(Permissions.Estoque.Ajuste)]
    public async Task<IActionResult> Ajuste(AjustarEstoqueDto dto)
    {
        var result = await service.AjustarAsync(dto);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("movimentacoes/{id:int}/cancelar")]
    [RequirePermission(Permissions.Estoque.CancelarMovimentacao)]
    public async Task<IActionResult> Cancelar(int id, MotivoDto dto)
    {
        var result = await service.CancelarAsync(id, dto);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { message = result.ErrorMessage })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
