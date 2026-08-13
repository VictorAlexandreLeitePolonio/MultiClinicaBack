using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador")]
[ApiController]
[Route("api/[controller]")]
public class FinancialController(IFinancialService service, IClinicExpenseService expenseService) : ControllerBase
{
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] FinancialBalanceQueryDto query)
    {
        var result = await service.GetBalanceAsync(query);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }

    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        => Ok((await expenseService.GetPagedAsync(startDate, endDate, page, pageSize)).Value);

    [HttpGet("expenses/{id}")]
    public async Task<IActionResult> GetExpense(int id)
    {
        var result = await expenseService.GetByIdAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense(CreateClinicExpenseDto dto)
    {
        var result = await expenseService.CreateAsync(dto);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetExpense), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("expenses/{id}")]
    public async Task<IActionResult> UpdateExpense(int id, UpdateClinicExpenseDto dto)
    {
        var result = await expenseService.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }

    [HttpDelete("expenses/{id}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var result = await expenseService.DeleteAsync(id);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return NoContent();
    }
}
