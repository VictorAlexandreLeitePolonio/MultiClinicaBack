using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Clinic;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/clinic/settings")]
public class ClinicSettingsController(IClinicSettingsService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.ClinicSettings.View)]
    public async Task<IActionResult> GetSettings()
    {
        var result = await service.GetCurrentClinicSettingsAsync();
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPut]
    [RequirePermission(Permissions.ClinicSettings.Update)]
    public async Task<IActionResult> UpdateSettings(UpdateClinicSettingsRequest request)
    {
        var result = await service.UpdateCurrentClinicSettingsAsync(request);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }
}
