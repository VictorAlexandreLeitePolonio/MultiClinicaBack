using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.PatientPortal;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/patient")]
[Authorize(AuthenticationSchemes = AuthSchemes.PatientAuth)]
public class PatientPortalController(IPatientPortalService service) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var result = await service.GetMeAsync();
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe(UpdatePatientMeDto dto)
    {
        var result = await service.UpdateMeAsync(dto);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("appointments/upcoming")]
    public async Task<IActionResult> Upcoming()
        => Ok((await service.GetUpcomingAppointmentsAsync()).Value);

    [HttpGet("appointments/history")]
    public async Task<IActionResult> History()
        => Ok((await service.GetHistoryAppointmentsAsync()).Value);

    [HttpGet("clinics")]
    public async Task<IActionResult> Clinics()
        => Ok((await service.GetClinicsAsync()).Value);
}
