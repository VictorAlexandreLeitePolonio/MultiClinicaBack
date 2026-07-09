using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/produtos")]
public class ProdutosController(IProdutoService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.Produtos.Visualizar)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? nome,
        [FromQuery] int? categoriaProdutoId,
        [FromQuery] bool? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        => Ok((await service.GetPagedAsync(nome, categoriaProdutoId, ativo, page, pageSize)).Value);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.Produtos.Visualizar)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    [RequirePermission(Permissions.Produtos.Criar)]
    public async Task<IActionResult> Create(CreateProdutoDto dto)
    {
        var result = await service.CreateAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.Produtos.Editar)]
    public async Task<IActionResult> Update(int id, UpdateProdutoDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { message = result.ErrorMessage })
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("{id:int}/inativar")]
    [RequirePermission(Permissions.Produtos.Inativar)]
    public async Task<IActionResult> Inativar(int id)
    {
        var result = await service.SetActiveAsync(id, false);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost("{id:int}/reativar")]
    [RequirePermission(Permissions.Produtos.Inativar)]
    public async Task<IActionResult> Reativar(int id)
    {
        var result = await service.SetActiveAsync(id, true);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }
}
