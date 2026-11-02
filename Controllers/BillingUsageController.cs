using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Services;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Authorize]
[Route("api/billing/usage")]
public class BillingUsageController(UsageService usage, IUsuarioLogadoService usuario) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await usage.GetAsync(usuario.ClinicaId));
}
