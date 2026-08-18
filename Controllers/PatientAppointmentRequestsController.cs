using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.AppointmentRequest;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/patient/appointment-requests")]
[Authorize(AuthenticationSchemes = AuthSchemes.PatientAuth)]
public class PatientAppointmentRequestsController(IAppointmentRequestService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateAppointmentRequestDto dto)
    {
        var result = await service.CreateAsync(dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound         => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.NotLinked        => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
                ErrorCodes.RequestsDisabled => Conflict(new { message = result.ErrorMessage }),
                _                           => BadRequest(new { message = result.ErrorMessage })
            };

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List()
        => Ok((await service.ListForPatientAsync()).Value);

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, ReasonDto dto)
    {
        var result = await service.CancelByPatientAsync(id, dto);
        return MapMutation(result);
    }

    private IActionResult MapMutation(Result<AppointmentRequestDto> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        return result.ErrorCode switch
        {
            ErrorCodes.NotFound      => NotFound(new { message = result.ErrorMessage }),
            ErrorCodes.InvalidStatus => Conflict(new { message = result.ErrorMessage }),
            _                        => BadRequest(new { message = result.ErrorMessage })
        };
    }
}
