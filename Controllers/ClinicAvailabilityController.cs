using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Availability;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/clinic/availability")]
public sealed class ClinicAvailabilityController(IAvailabilityService service) : ControllerBase
{
    [HttpGet("settings")]
    [RequirePermission(Permissions.ClinicSettings.View)]
    public async Task<IActionResult> GetSettings()
        => Map(await service.GetSettingsAsync());

    [HttpPut("settings")]
    [RequirePermission(Permissions.ClinicSettings.Update)]
    public async Task<IActionResult> UpdateSettings(UpdateAvailabilitySettingsDto dto)
        => Map(await service.UpdateSettingsAsync(dto));

    [HttpGet("professionals/{professionalId:int}")]
    [RequirePermission(Permissions.ClinicSettings.View)]
    public async Task<IActionResult> GetProfessional(int professionalId)
        => Map(await service.GetProfessionalAsync(professionalId));

    [HttpPut("professionals/{professionalId:int}")]
    [RequirePermission(Permissions.ClinicSettings.Update)]
    public async Task<IActionResult> ReplaceProfessional(
        int professionalId, IReadOnlyList<ProfessionalAvailabilityRangeDto> ranges)
        => Map(await service.ReplaceProfessionalAsync(professionalId, ranges));

    private IActionResult Map<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);
        return result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { code = result.ErrorCode, message = result.ErrorMessage })
            : BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
    }
}
