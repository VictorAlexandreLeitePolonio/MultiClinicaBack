using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.DTOs.SessionTypes;
using MultiClinica.API.Services;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador")]
[ApiController]
[Route("api/session-types")]
public class SessionTypesController(SessionTypeService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search) =>
        Ok(await service.GetAllAsync(search));

    [HttpPost]
    public async Task<IActionResult> Create(CreateSessionTypeDto dto)
    {
        var result = await service.CreateAsync(dto);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }
}
