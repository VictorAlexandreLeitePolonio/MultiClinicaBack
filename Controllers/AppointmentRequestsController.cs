using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.AppointmentRequest;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/appointment-requests")]
[Authorize(Roles = "Administrador,Profissional,Recepcao")]
public class AppointmentRequestsController(IAppointmentRequestService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
        => Ok((await service.ListForClinicAsync()).Value);

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var result = await service.GetForClinicAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPatch("{id}/accept")]
    public async Task<IActionResult> Accept(int id, AcceptAppointmentRequestDto dto)
        => MapMutation(await service.AcceptAsync(id, dto));

    [HttpPatch("{id}/reject")]
    public async Task<IActionResult> Reject(int id, ReasonDto dto)
        => MapMutation(await service.RejectAsync(id, dto));

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, ReasonDto dto)
        => MapMutation(await service.CancelByClinicAsync(id, dto));

    private IActionResult MapMutation(Result<AppointmentRequestDto> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        return result.ErrorCode switch
        {
            ErrorCodes.NotFound      => NotFound(new { message = result.ErrorMessage }),
            ErrorCodes.Forbidden     => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            ErrorCodes.InvalidStatus => Conflict(new { message = result.ErrorMessage }),
            _                        => BadRequest(new { message = result.ErrorMessage })
        };
    }
}
