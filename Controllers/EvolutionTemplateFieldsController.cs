using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador,Profissional,Recepcao")]
[ApiController]
public class EvolutionTemplateFieldsController(IEvolutionService service) : ControllerBase
{
    [HttpGet("api/evolution-templates/{templateId}/fields")]
    public async Task<IActionResult> GetFields(int templateId)
    {
        var result = await service.GetFieldsAsync(templateId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost("api/evolution-templates/{templateId}/fields")]
    public async Task<IActionResult> CreateField(int templateId, CreateEvolutionTemplateFieldDto dto)
    {
        var result = await service.CreateFieldAsync(templateId, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetFields), new { templateId }, result.Value);
    }

    [Authorize(Roles = "Administrador")]
    [HttpPatch("api/evolution-template-fields/{fieldId}")]
    public async Task<IActionResult> UpdateField(int fieldId, UpdateEvolutionTemplateFieldDto dto)
    {
        var result = await service.UpdateFieldAsync(fieldId, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return Ok(new { message = "Campo de evolução atualizado com sucesso." });
    }

    [Authorize(Roles = "Administrador")]
    [HttpDelete("api/evolution-template-fields/{fieldId}")]
    public async Task<IActionResult> DeleteField(int fieldId)
    {
        var result = await service.DeactivateFieldAsync(fieldId);
        if (!result.IsSuccess)
            return NotFound(new { message = result.ErrorMessage });

        return NoContent();
    }
}
