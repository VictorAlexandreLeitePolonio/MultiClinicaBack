using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Services;

namespace MultiClinica.API.Controllers;

// Prontuário do paciente: medições e linha do tempo.
[ApiController]
[Route("api/patients/{patientId:int}")]
public class PatientRecordController(PatientRecordService service) : ControllerBase
{
    [Authorize(Roles = "Administrador,Profissional")]
    [HttpGet("measurements")]
    public async Task<IActionResult> GetMeasurements(int patientId)
    {
        var result = await service.GetMeasurementsAsync(patientId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { code = result.ErrorCode, message = result.ErrorMessage });
    }

    [Authorize(Roles = "Administrador,Profissional,Recepcao")]
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline(
        int patientId,
        [FromQuery] DateTime? before,
        [FromQuery] int take = 30)
    {
        var result = await service.GetTimelineAsync(patientId, before, take);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { code = result.ErrorCode, message = result.ErrorMessage });
    }
}
