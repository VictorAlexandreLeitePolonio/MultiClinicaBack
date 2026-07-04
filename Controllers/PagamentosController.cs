using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/pagamentos")]
public class PagamentosController(IPagamentoContaPagarService service) : ControllerBase
{
    [HttpPost]
    [RequirePermission(Permissions.ContasPagar.RegistrarPagamento)]
    public async Task<IActionResult> Registrar(CreatePagamentoContaPagarDto dto)
    {
        var result = await service.RegistrarAsync(dto);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("{id:int}/estornar")]
    [RequirePermission(Permissions.ContasPagar.EstornarPagamento)]
    public async Task<IActionResult> Estornar(int id, EstornarPagamentoContaPagarDto dto)
    {
        var result = await service.EstornarAsync(id, dto);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { message = result.ErrorMessage })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
