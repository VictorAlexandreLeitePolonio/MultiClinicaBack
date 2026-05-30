using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Plans;
using ProjetoLP.API.Models;
using ProjetoLP.API.Services.Interfaces;

namespace ProjetoLP.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class PlansController(IPlanService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPlans(
        [FromQuery] TipoPlano? tipoPlano,
        [FromQuery] TipoSessao? tipoSessao,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await service.GetPagedAsync(tipoPlano, tipoSessao, isActive, page, pageSize);
        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlan(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlan(CreatePlanDto dto)
    {
        var result = await service.CreateAsync(dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.DuplicateName => Conflict(new { message = result.ErrorMessage }),
                _                        => BadRequest(new { message = result.ErrorMessage })
            };

        return CreatedAtAction(nameof(GetPlan), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlan(int id, UpdatePlanDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound      => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.DuplicateName => Conflict(new { message = result.ErrorMessage }),
                _                        => BadRequest(new { message = result.ErrorMessage })
            };

        return Ok(result.Value);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ChangeStatus(int id)
    {
        var result = await service.ToggleStatusAsync(id);
        if (!result.IsSuccess)
            return NotFound(new { message = result.ErrorMessage });

        // Busca o plano atualizado para retornar o status
        var planResult = await service.GetByIdAsync(id);
        return Ok(new { id, isActive = planResult.Value!.IsActive });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlan(int id)
    {
        var result = await service.DeleteAsync(id);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound            => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.HasAssociatedRecords => BadRequest(new { message = result.ErrorMessage }),
                _                              => BadRequest(new { message = result.ErrorMessage })
            };

        return NoContent();
    }
}
