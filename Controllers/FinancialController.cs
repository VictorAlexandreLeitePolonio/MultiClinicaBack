using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador")]
[ApiController]
[Route("api/[controller]")]
public class FinancialController(IFinancialService service) : ControllerBase
{
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] FinancialBalanceQueryDto query)
    {
        var result = await service.GetBalanceAsync(query);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }
}
