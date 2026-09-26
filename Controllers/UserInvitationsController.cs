using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.User;
using MultiClinica.API.Services;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/user-invitations")]
[EnableRateLimiting("auth")]
public class UserInvitationsController(UserInvitationService service) : ControllerBase
{
    [Authorize(Roles = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Invite(InviteUserDto dto) => Map(await service.InviteAsync(dto));

    [Authorize(Roles = "Administrador")]
    [HttpPost("{id:int}/resend")]
    public async Task<IActionResult> Resend(int id) => Map(await service.ResendAsync(id));

    [AllowAnonymous]
    [HttpPost("accept")]
    public async Task<IActionResult> Accept(AcceptUserInvitationDto dto) =>
        await service.AcceptAsync(dto)
            ? Ok(new { message = "Convite aceito. Sua senha foi definida." })
            : BadRequest(new { message = "Convite inválido ou expirado. Solicite um novo convite ao administrador." });

    private IActionResult Map(Result<UserInvitationResponseDto> result) =>
        result.IsSuccess ? Ok(result.Value) : result.ErrorCode switch
        {
            ErrorCodes.Forbidden => Forbid(),
            ErrorCodes.NotFound => NotFound(new { message = result.ErrorMessage }),
            ErrorCodes.DuplicateEmail => Conflict(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage }),
        };
}
