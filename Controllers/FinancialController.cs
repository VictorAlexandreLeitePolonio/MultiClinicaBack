using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs.Financial;
using ProjetoLP.API.Services.Interfaces;

namespace ProjetoLP.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class FinancialController(IFinancialService service) : ControllerBase
{
    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] string? month,
        [FromQuery] string? title,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await service.GetExpensesPagedAsync(month, title, page, pageSize);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }

    [HttpGet("expenses/{id}")]
    public async Task<IActionResult> GetExpense(int id)
    {
        var result = await service.GetExpenseByIdAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense(CreateExpenseDto dto)
    {
        var result = await service.CreateExpenseAsync(dto);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetExpense), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("expenses/{id}")]
    public async Task<IActionResult> UpdateExpense(int id, UpdateExpenseDto dto)
    {
        var result = await service.UpdateExpenseAsync(id, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }

    [HttpDelete("expenses/{id}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var result = await service.DeleteExpenseAsync(id);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return NoContent();
    }

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
