using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/fornecedores")]
public class FornecedoresController(IFornecedorService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.Fornecedores.Visualizar)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? nome,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        => Ok((await service.GetPagedAsync(nome, page, pageSize)).Value);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.Fornecedores.Visualizar)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    [RequirePermission(Permissions.Fornecedores.Criar)]
    public async Task<IActionResult> Create(CreateFornecedorDto dto)
    {
        var result = await service.CreateAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }
}
