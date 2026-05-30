using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.User;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador")]
[ApiController]
[Route("api/[controller]")]
public class UsersController(IUserService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await service.GetPagedAsync(page, pageSize);
        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserDto dto)
    {
        var result = await service.CreateAsync(dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.DuplicateEmail    => Conflict(new { message = result.ErrorMessage }),
                ErrorCodes.InvalidPassword   => BadRequest(new { message = result.ErrorMessage }),
                ErrorCodes.Forbidden          => Forbid(),
                _                            => BadRequest(new { message = result.ErrorMessage })
            };

        return CreatedAtAction(nameof(GetUser), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound       => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.DuplicateEmail => Conflict(new { message = result.ErrorMessage }),
                _                         => BadRequest(new { message = result.ErrorMessage })
            };

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var result = await service.DeleteAsync(id);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound            => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.LastAdmin           => BadRequest(new { message = result.ErrorMessage }),
                ErrorCodes.HasAssociatedRecords => Conflict(new { message = result.ErrorMessage }),
                _                              => BadRequest(new { message = result.ErrorMessage })
            };

        return NoContent();
    }
}
