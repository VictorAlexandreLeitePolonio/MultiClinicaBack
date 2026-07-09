using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/recebimentos")]
public class RecebimentosController(IRecebimentoService service) : ControllerBase
{
    [HttpGet("~/api/contas-receber/{contaReceberId:int}/recebimentos")]
    [RequirePermission(Permissions.ContasReceber.Visualizar)]
    public async Task<IActionResult> GetByContaReceber(int contaReceberId)
    {
        var result = await service.GetByContaReceberAsync(contaReceberId);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    [RequirePermission(Permissions.ContasReceber.RegistrarRecebimento)]
    public async Task<IActionResult> Registrar(CreateRecebimentoDto dto)
    {
        var result = await service.RegistrarAsync(dto);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("{id:int}/estornar")]
    [RequirePermission(Permissions.ContasReceber.EstornarRecebimento)]
    public async Task<IActionResult> Estornar(int id, EstornarRecebimentoDto dto)
    {
        var result = await service.EstornarAsync(id, dto);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { message = result.ErrorMessage })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
