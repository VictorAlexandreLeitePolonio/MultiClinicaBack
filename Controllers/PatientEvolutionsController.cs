using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

// Evoluções pertencem ao paciente (sem acompanhamento); conteúdo clínico: só Administrador e Profissional.
[Authorize(Roles = "Administrador,Profissional")]
[ApiController]
[Route("api/patients/{patientId}/evolutions")]
public class PatientEvolutionsController(IEvolutionService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetEvolutions(
        int patientId,
        [FromQuery] int? appointmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await service.GetEvolutionsAsync(patientId, appointmentId, page, pageSize);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("{evolutionId}")]
    public async Task<IActionResult> GetEvolution(int patientId, int evolutionId)
    {
        var result = await service.GetEvolutionAsync(patientId, evolutionId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> CreateEvolution(int patientId, CreatePatientEvolutionDto dto)
    {
        var result = await service.CreateEvolutionAsync(patientId, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { code = result.ErrorCode, message = result.ErrorMessage })
                : BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });

        return CreatedAtAction(
            nameof(GetEvolution),
            new { patientId, evolutionId = result.Value!.Id },
            result.Value);
    }

    [HttpPatch("{evolutionId}")]
    public async Task<IActionResult> UpdateEvolution(int patientId, int evolutionId, UpdatePatientEvolutionDto dto)
    {
        var result = await service.UpdateEvolutionAsync(patientId, evolutionId, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { code = result.ErrorCode, message = result.ErrorMessage })
                : BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });

        return Ok(new { message = "Evolução atualizada com sucesso." });
    }

    [HttpDelete("{evolutionId}")]
    public async Task<IActionResult> DeleteEvolution(int patientId, int evolutionId)
    {
        var result = await service.DeleteEvolutionAsync(patientId, evolutionId);
        if (!result.IsSuccess)
            return NotFound(new { message = result.ErrorMessage });

        return NoContent();
    }
}
