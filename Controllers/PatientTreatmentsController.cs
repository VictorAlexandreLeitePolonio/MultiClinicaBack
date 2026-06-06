using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador,Profissional,Recepcao")]
[ApiController]
[Route("api/patients/{patientId}/treatments")]
public class PatientTreatmentsController(IEvolutionService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTreatments(int patientId)
    {
        var result = await service.GetTreatmentsAsync(patientId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("{treatmentId}")]
    public async Task<IActionResult> GetTreatment(int patientId, int treatmentId)
    {
        var result = await service.GetTreatmentAsync(patientId, treatmentId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [Authorize(Roles = "Administrador,Profissional")]
    [HttpPost]
    public async Task<IActionResult> CreateTreatment(int patientId, CreatePatientTreatmentDto dto)
    {
        var result = await service.CreateTreatmentAsync(patientId, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetTreatment), new { patientId, treatmentId = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = "Administrador,Profissional")]
    [HttpPatch("{treatmentId}")]
    public async Task<IActionResult> UpdateTreatment(int patientId, int treatmentId, UpdatePatientTreatmentDto dto)
    {
        var result = await service.UpdateTreatmentAsync(patientId, treatmentId, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return Ok(new { message = "Acompanhamento atualizado com sucesso." });
    }
}
