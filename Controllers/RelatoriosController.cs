using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/relatorios")]
public class RelatoriosController(IRelatorioService service) : ControllerBase
{
    [HttpGet("faturamento")]
    [RequirePermission(Permissions.Relatorios.Faturamento)]
    public async Task<IActionResult> Faturamento(
        [FromQuery] DateTime de,
        [FromQuery] DateTime ate,
        [FromQuery] AgrupamentoRelatorio agruparPor = AgrupamentoRelatorio.Periodo)
    {
        var result = await service.GetFaturamentoAsync(de, ate, agruparPor);
        return ToResponse(result);
    }

    [HttpGet("despesas")]
    [RequirePermission(Permissions.Relatorios.Despesas)]
    public async Task<IActionResult> Despesas([FromQuery] DateTime de, [FromQuery] DateTime ate)
    {
        var result = await service.GetDespesasAsync(de, ate);
        return ToResponse(result);
    }

    [HttpGet("resultado")]
    [RequirePermission(Permissions.Relatorios.Lucro)]
    public async Task<IActionResult> Resultado([FromQuery] DateTime de, [FromQuery] DateTime ate)
    {
        var result = await service.GetResultadoAsync(de, ate);
        return ToResponse(result);
    }

    [HttpGet("produtos-mais-movimentados")]
    [RequirePermission(Permissions.Relatorios.Estoque)]
    public async Task<IActionResult> ProdutosMaisMovimentados(
        [FromQuery] DateTime de,
        [FromQuery] DateTime ate,
        [FromQuery] int limite = 10)
    {
        var result = await service.GetProdutosMaisMovimentadosAsync(de, ate, limite);
        return ToResponse(result);
    }

    private IActionResult ToResponse<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.ErrorMessage });
}
