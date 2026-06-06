using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador,Profissional,Recepcao")]
[ApiController]
[Route("api/dashboard")]
public class DashboardController(IEvolutionDashboardService evolutionDashboardService) : ControllerBase
{
    [HttpGet("evolution-summary")]
    public async Task<IActionResult> GetEvolutionSummary()
    {
        var summary = await evolutionDashboardService.GetSummaryAsync();
        return Ok(summary);
    }
}
