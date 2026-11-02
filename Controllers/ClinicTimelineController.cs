using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Services;

namespace MultiClinica.API.Controllers;

// Linha do tempo da clínica inteira (Administrador e Profissional).
[ApiController]
[Authorize(Roles = "Administrador,Profissional")]
[Route("api/patient-records")]
public class ClinicTimelineController(PatientRecordService service) : ControllerBase
{
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] DateTime? before,
        [FromQuery] int take = 30)
    {
        var result = await service.GetClinicTimelineAsync(before, take);
        return Ok(result.Value);
    }
}
