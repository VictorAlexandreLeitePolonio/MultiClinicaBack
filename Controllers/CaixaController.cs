using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/caixa")]
public class CaixaController(ICaixaService service) : ControllerBase
{
    [HttpGet("atual")]
    [RequirePermission(Permissions.Caixa.Visualizar)]
    public async Task<IActionResult> GetAtual()
    {
        var result = await service.GetAtualAsync();
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.Caixa.Visualizar)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("{id:int}/movimentacoes")]
    [RequirePermission(Permissions.Caixa.VisualizarMovimentacoes)]
    public async Task<IActionResult> GetMovimentacoes(int id)
    {
        var result = await service.GetMovimentacoesAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("{id:int}/resumo")]
    [RequirePermission(Permissions.Caixa.VisualizarResumo)]
    public async Task<IActionResult> GetResumo(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost("abrir")]
    [RequirePermission(Permissions.Caixa.Abrir)]
    public async Task<IActionResult> Abrir(AbrirCaixaDto dto)
    {
        var result = await service.AbrirAsync(dto);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("{id:int}/fechar")]
    [RequirePermission(Permissions.Caixa.Fechar)]
    public async Task<IActionResult> Fechar(int id, FecharCaixaDto dto)
    {
        var result = await service.FecharAsync(id, dto);
        return ToMutationResult(result);
    }

    [HttpPost("{id:int}/reabrir")]
    [RequirePermission(Permissions.Caixa.Reabrir)]
    public async Task<IActionResult> Reabrir(int id, MotivoDto dto)
    {
        var result = await service.ReabrirAsync(id, dto);
        return ToMutationResult(result);
    }

    [HttpPost("{id:int}/ajustar")]
    [RequirePermission(Permissions.Caixa.Ajustar)]
    public async Task<IActionResult> Ajustar(int id, AjustarCaixaDto dto)
    {
        var result = await service.AjustarAsync(id, dto);
        return ToMutationResult(result);
    }

    [HttpPost("{id:int}/cancelar")]
    [RequirePermission(Permissions.Caixa.Cancelar)]
    public async Task<IActionResult> Cancelar(int id, MotivoDto dto)
    {
        var result = await service.CancelarAsync(id, dto);
        return ToMutationResult(result);
    }

    private IActionResult ToMutationResult<T>(Result<T> result) =>
        result.IsSuccess
            ? Ok(result.Value)
            : result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });
}
