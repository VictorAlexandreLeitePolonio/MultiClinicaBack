using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador")]
[ApiController]
[Route("api/[controller]")]
public class FinancialController(IFinancialService service) : ControllerBase
{
    [HttpGet("balance/history")]
    public async Task<IActionResult> GetBalanceHistory([FromQuery] int months = 6)
    {
        var result = await service.GetBalanceHistoryAsync(months);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }

    [HttpGet("balance/{month}")]
    public async Task<IActionResult> GetBalance(string month)
    {
        var result = await service.GetBalanceAsync(month);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }
}
