using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/public/clinics")]
[AllowAnonymous]
public class PublicClinicsController(IClinicProfileService service) : ControllerBase
{
    /// <summary>Vitrine pública — clínicas IsPublic ativas, ordenadas por curtidas.</summary>
    [HttpGet]
    public async Task<IActionResult> GetShowcase([FromQuery] int limit = 12)
        => Ok((await service.GetPublicShowcaseAsync(limit)).Value);

    /// <summary>Perfil público — apenas clínicas IsPublic, ativas e não excluídas.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var result = await service.GetPublicBySlugAsync(slug);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }
}
