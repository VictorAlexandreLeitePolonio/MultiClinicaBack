using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Patient;
using ProjetoLP.API.Models;
using ProjetoLP.API.Services.Interfaces;

namespace ProjetoLP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PatientsController(IPatientService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPatients(
        [FromQuery] string? name,
        [FromQuery] bool? isActive,
        [FromQuery] AppointmentStatus? appointmentStatus,
        [FromQuery] PaymentStatus? paymentStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await service.GetPagedAsync(
            name, isActive, appointmentStatus, paymentStatus, page, pageSize);
        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPatient(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet("{id}/profile")]
    public async Task<IActionResult> GetPatientProfile(int id)
    {
        var result = await service.GetProfileAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePatient(CreatePatientDto dto)
    {
        var result = await service.CreateAsync(dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.DuplicateEmail => Conflict(new { message = result.ErrorMessage }),
                ErrorCodes.DuplicateCpf   => Conflict(new { message = result.ErrorMessage }),
                _                         => BadRequest(new { message = result.ErrorMessage })
            };

        return CreatedAtAction(nameof(GetPatient), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePatient(int id, UpdatePatientDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound       => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.DuplicateEmail => Conflict(new { message = result.ErrorMessage }),
                ErrorCodes.DuplicateCpf   => Conflict(new { message = result.ErrorMessage }),
                _                         => BadRequest(new { message = result.ErrorMessage })
            };

        return Ok(new { message = "Paciente atualizado com sucesso." });
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ChangeStatus(int id)
    {
        var result = await service.ToggleStatusAsync(id);
        if (!result.IsSuccess)
            return NotFound(new { message = result.ErrorMessage });

        // Busca o paciente atualizado para retornar o status
        var patientResult = await service.GetByIdAsync(id);
        return Ok(new { id, isActive = patientResult.Value!.IsActive });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePatient(int id)
    {
        var result = await service.DeleteAsync(id);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound            => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.HasAssociatedRecords => Conflict(new { message = result.ErrorMessage }),
                _                              => BadRequest(new { message = result.ErrorMessage })
            };

        return NoContent();
    }
}
