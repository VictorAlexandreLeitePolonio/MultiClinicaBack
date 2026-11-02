using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.User;
using MultiClinica.API.Services;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/user-invitations")]
[EnableRateLimiting("auth")]
public class UserInvitationsController(UserInvitationService service, AppDbContext db, IUsuarioLogadoService usuario)
    : ControllerBase
{
    [Authorize(Roles = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Invite(InviteUserDto dto) => await Map(await service.InviteAsync(dto));

    [Authorize(Roles = "Administrador")]
    [HttpPost("{id:int}/resend")]
    public async Task<IActionResult> Resend(int id) => await Map(await service.ResendAsync(id));

    [AllowAnonymous]
    [HttpPost("accept")]
    public async Task<IActionResult> Accept(AcceptUserInvitationDto dto)
    {
        var result = await service.AcceptAsync(dto);
        if (result.IsSuccess) return Ok(new { message = "Convite aceito. Sua senha foi definida." });
        return result.ErrorCode == ErrorCodes.ProfessionalLimit
            ? Conflict(new { code = result.ErrorCode, message = result.ErrorMessage })
            : BadRequest(new { message = result.ErrorMessage });
    }

    private async Task<IActionResult> Map(Result<UserInvitationResponseDto> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        if (result.ErrorCode == ErrorCodes.ProfessionalLimit)
        {
            var clinic = await db.Clinicas.Where(c => c.Id == usuario.ClinicaId)
                .Select(c => new { c.Plan, c.ExtraProfessionals }).SingleAsync();
            return PlanErrors.ProfessionalLimit(
                PlanMatrix.ProfessionalLimit(clinic.Plan, clinic.ExtraProfessionals) ?? 0);
        }
        return result.ErrorCode switch
        {
            ErrorCodes.Forbidden => Forbid(),
            ErrorCodes.NotFound => NotFound(new { message = result.ErrorMessage }),
            ErrorCodes.DuplicateEmail => Conflict(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage }),
        };
    }
}
