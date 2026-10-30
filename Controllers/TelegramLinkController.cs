using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.Services.Agent;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

// ponytail: só Administrador no MVP; outras roles exigem filtrar as tools por permissão.
[Authorize(Roles = "Administrador")]
[RequireFeature(Features.OwnerCopilot)]
[ApiController]
[Route("api/me/telegram-link")]
public class TelegramLinkController(TelegramLinkService links, IUsuarioLogadoService usuario) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Status() => Ok(new { connected = await links.IsConnectedAsync(usuario.UserId) });

    [HttpPost]
    public async Task<IActionResult> Create() => Ok(new { url = await links.CreateLinkAsync(usuario.UserId) });

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        await links.UnlinkAsync(usuario.UserId);
        return NoContent();
    }
}
