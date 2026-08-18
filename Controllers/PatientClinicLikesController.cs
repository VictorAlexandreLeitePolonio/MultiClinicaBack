using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/patient/clinics/{clinicId}/like")]
[Authorize(AuthenticationSchemes = AuthSchemes.PatientAuth)]
public class PatientClinicLikesController(IClinicLikeService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Like(int clinicId)
    {
        var result = await service.LikeAsync(clinicId);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpDelete]
    public async Task<IActionResult> Unlike(int clinicId)
    {
        var result = await service.UnlikeAsync(clinicId);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }
}
