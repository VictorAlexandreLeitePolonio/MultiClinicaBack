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
[Route("api/compras")]
public class ComprasController(CompraService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.Compras.Visualizar)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? fornecedorId,
        [FromQuery] StatusCompra? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        => Ok((await service.GetPagedAsync(fornecedorId, status, page, pageSize)).Value);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.Compras.Visualizar)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    [RequirePermission(Permissions.Compras.Criar)]
    public async Task<IActionResult> Create(CreateCompraDto dto)
    {
        var result = await service.CreateAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.Compras.Editar)]
    public async Task<IActionResult> Update(int id, UpdateCompraDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { message = result.ErrorMessage })
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("{id:int}/aprovar")]
    [RequirePermission(Permissions.Compras.Aprovar)]
    public async Task<IActionResult> Aprovar(int id)
    {
        var result = await service.AprovarAsync(id);
        return ToActionResult(result);
    }

    [HttpPost("{id:int}/receber")]
    [RequirePermission(Permissions.Compras.ReceberProdutos)]
    public async Task<IActionResult> Receber(int id)
    {
        var result = await service.ReceberAsync(id);
        return ToActionResult(result);
    }

    [HttpPost("{id:int}/cancelar")]
    [RequirePermission(Permissions.Compras.Cancelar)]
    public async Task<IActionResult> Cancelar(int id, MotivoDto dto)
    {
        var result = await service.CancelarAsync(id, dto);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result<CompraResponseDto> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { message = result.ErrorMessage })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
