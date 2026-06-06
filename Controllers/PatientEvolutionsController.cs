using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador,Profissional,Recepcao")]
[ApiController]
[Route("api/patients/{patientId}/treatments/{treatmentId}/evolutions")]
public class PatientEvolutionsController(IEvolutionService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetEvolutions(
        int patientId,
        int treatmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await service.GetEvolutionsAsync(patientId, treatmentId, page, pageSize);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("{evolutionId}")]
    public async Task<IActionResult> GetEvolution(int patientId, int treatmentId, int evolutionId)
    {
        var result = await service.GetEvolutionAsync(patientId, treatmentId, evolutionId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("~/api/patients/{patientId}/treatments/{treatmentId}/progress")]
    public async Task<IActionResult> GetProgress(int patientId, int treatmentId)
    {
        var result = await service.GetTreatmentProgressAsync(patientId, treatmentId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [Authorize(Roles = "Administrador,Profissional")]
    [HttpPost]
    public async Task<IActionResult> CreateEvolution(int patientId, int treatmentId, CreatePatientEvolutionDto dto)
    {
        var result = await service.CreateEvolutionAsync(patientId, treatmentId, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(
            nameof(GetEvolution),
            new { patientId, treatmentId, evolutionId = result.Value!.Id },
            result.Value);
    }

    [Authorize(Roles = "Administrador,Profissional")]
    [HttpPatch("{evolutionId}")]
    public async Task<IActionResult> UpdateEvolution(int patientId, int treatmentId, int evolutionId, UpdatePatientEvolutionDto dto)
    {
        var result = await service.UpdateEvolutionAsync(patientId, treatmentId, evolutionId, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return Ok(new { message = "Evolução atualizada com sucesso." });
    }

    [Authorize(Roles = "Administrador,Profissional")]
    [HttpDelete("{evolutionId}")]
    public async Task<IActionResult> DeleteEvolution(int patientId, int treatmentId, int evolutionId)
    {
        var result = await service.DeleteEvolutionAsync(patientId, treatmentId, evolutionId);
        if (!result.IsSuccess)
            return NotFound(new { message = result.ErrorMessage });

        return NoContent();
    }
}
