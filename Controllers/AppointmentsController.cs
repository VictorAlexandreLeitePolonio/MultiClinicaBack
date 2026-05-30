using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Appointment;
using ProjetoLP.API.Models;
using ProjetoLP.API.Services.Interfaces;

namespace ProjetoLP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(IAppointmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] AppointmentStatus? status,
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] string? PatientName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await service.GetPagedAsync(
            status, date, dateFrom, dateTo, PatientName, page, pageSize);
        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAppointment(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointment(CreateAppointmentDto dto)
    {
        var result = await service.CreateAsync(dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound         => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.InactivePatient  => BadRequest(new { message = result.ErrorMessage }),
                ErrorCodes.InvalidDate      => BadRequest(new { message = result.ErrorMessage }),
                _                           => BadRequest(new { message = result.ErrorMessage })
            };

        return CreatedAtAction(nameof(GetAppointment), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAppointment(int id, UpdateAppointmentDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound      => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.CannotModify  => BadRequest(new { message = result.ErrorMessage }),
                _                        => BadRequest(new { message = result.ErrorMessage })
            };

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAppointment(int id)
    {
        var result = await service.DeleteAsync(id);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound      => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.CannotDelete  => BadRequest(new { message = result.ErrorMessage }),
                _                        => BadRequest(new { message = result.ErrorMessage })
            };

        return NoContent();
    }
}
