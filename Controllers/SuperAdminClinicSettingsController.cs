using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Clinic;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/superadmin/clinics")]
public class SuperAdminClinicSettingsController(IClinicSettingsService service) : ControllerBase
{
    [HttpGet("{clinicId:int}/settings")]
    public async Task<IActionResult> GetClinicSettings(int clinicId)
    {
        var result = await service.GetClinicSettingsAsSuperAdminAsync(clinicId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPut("{clinicId:int}/settings")]
    public async Task<IActionResult> UpdateClinicSettings(int clinicId, UpdateClinicSettingsRequest request)
    {
        var result = await service.UpdateClinicSettingsAsSuperAdminAsync(clinicId, request);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }
}
