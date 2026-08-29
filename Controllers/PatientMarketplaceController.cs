using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.DTOs.Marketplace;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/patient/marketplace")]
[Authorize(AuthenticationSchemes = AuthSchemes.PatientAuth)]
public sealed class PatientMarketplaceController(
    IMarketplaceService service,
    IAvailabilityService availability,
    IPatientAccountLoggedService patient) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
        => Ok((await service.GetCategoriesAsync()).Value);

    [HttpGet("clinics")]
    public async Task<IActionResult> GetClinics([FromQuery] MarketplaceClinicQuery query)
        => Ok((await service.GetClinicsAsync(query)).Value);

    [HttpGet("clinics/{clinicId:int}")]
    public async Task<IActionResult> GetClinic(int clinicId)
    {
        var result = await service.GetClinicAsync(clinicId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("clinics/{clinicId:int}/availability")]
    public async Task<IActionResult> GetAvailability(int clinicId, [FromQuery] DateOnly date)
    {
        var result = await availability.GetClinicAvailabilityAsync(clinicId, date, patient.PatientAccountId);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { code = result.ErrorCode, message = result.ErrorMessage });
    }
}
