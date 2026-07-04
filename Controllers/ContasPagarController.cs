using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/contas-pagar")]
public class ContasPagarController(IContaPagarService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.ContasPagar.Visualizar)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? fornecedorId,
        [FromQuery] StatusContaPagar? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        => Ok((await service.GetPagedAsync(fornecedorId, status, page, pageSize)).Value);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.ContasPagar.Visualizar)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    [RequirePermission(Permissions.ContasPagar.Criar)]
    public async Task<IActionResult> Create(CreateContaPagarDto dto)
    {
        var result = await service.CreateAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.ContasPagar.Editar)]
    public async Task<IActionResult> Update(int id, UpdateContaPagarDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { message = result.ErrorMessage })
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("{id:int}/cancelar")]
    [RequirePermission(Permissions.ContasPagar.Cancelar)]
    public async Task<IActionResult> Cancelar(int id)
    {
        var result = await service.CancelarAsync(id);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { message = result.ErrorMessage })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
