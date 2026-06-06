using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador,Profissional,Recepcao")]
[ApiController]
[Route("api/evolution-templates")]
public class EvolutionTemplatesController(IEvolutionTemplateService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTemplates([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await service.GetPagedAsync(page, pageSize);
        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTemplate(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> CreateTemplate(CreateEvolutionTemplateDto dto)
    {
        var result = await service.CreateAsync(dto);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetTemplate), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = "Administrador")]
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateTemplate(int id, UpdateEvolutionTemplateDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return Ok(new { message = "Modelo de evolução atualizado com sucesso." });
    }

    [Authorize(Roles = "Administrador")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var result = await service.DeactivateAsync(id);
        if (!result.IsSuccess)
            return NotFound(new { message = result.ErrorMessage });

        return NoContent();
    }
}
