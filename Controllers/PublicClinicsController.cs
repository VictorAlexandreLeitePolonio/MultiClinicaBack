using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MultiClinica.API.DTOs.Marketplace;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/public/clinics")]
[AllowAnonymous]
public class PublicClinicsController(
    IClinicProfileService service,
    IMarketplaceService marketplace) : ControllerBase
{
    /// <summary>Vitrine pública — clínicas IsPublic ativas, ordenadas por curtidas.</summary>
    [HttpGet]
    public async Task<IActionResult> GetShowcase([FromQuery] int limit = 12)
        => Ok((await service.GetPublicShowcaseAsync(limit)).Value);

    /// <summary>Hub público — busca anônima; o id do paciente é sempre null nesta rota.</summary>
    [HttpGet("search")]
    [EnableRateLimiting("public-hub")]
    public async Task<IActionResult> Search([FromQuery] MarketplaceClinicQuery query)
        => Ok((await marketplace.GetClinicsAsync(query, patientAccountId: null)).Value);

    /// <summary>Hub público — catálogo de categorias ativas.</summary>
    [HttpGet("categories")]
    [EnableRateLimiting("public-hub")]
    public async Task<IActionResult> GetCategories()
        => Ok((await marketplace.GetCategoriesAsync()).Value);

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
