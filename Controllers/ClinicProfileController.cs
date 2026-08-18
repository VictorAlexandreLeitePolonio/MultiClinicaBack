using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Clinic;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/clinic")]
[Authorize(Roles = "Administrador")]
public class ClinicProfileController(IClinicProfileService service) : ControllerBase
{
    // ── Categorias ─────────────────────────────────────────────────────────────

    [HttpGet("categories/catalog")]
    public async Task<IActionResult> Catalog()
        => Ok((await service.GetCategoryCatalogAsync()).Value);

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await service.GetClinicCategoriesAsync();
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPut("categories")]
    public async Task<IActionResult> SetCategories(SetClinicCategoriesRequest request)
    {
        var result = await service.SetClinicCategoriesAsync(request);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });
        return Ok(result.Value);
    }

    // ── Horários ───────────────────────────────────────────────────────────────

    [HttpGet("business-hours")]
    public async Task<IActionResult> GetBusinessHours()
        => Ok((await service.GetBusinessHoursAsync()).Value);

    [HttpPost("business-hours")]
    public async Task<IActionResult> AddBusinessHour(CreateBusinessHourRequest request)
    {
        var result = await service.AddBusinessHourAsync(request);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpDelete("business-hours/{id}")]
    public async Task<IActionResult> DeleteBusinessHour(int id)
    {
        var result = await service.DeleteBusinessHourAsync(id);
        return result.IsSuccess ? NoContent() : NotFound(new { message = result.ErrorMessage });
    }
}
