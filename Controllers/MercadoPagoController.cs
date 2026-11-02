using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.MercadoPago;
using MultiClinica.API.Services.Interfaces;
using MultiClinica.API.Services.MercadoPago;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador")]
[RequireFeature(Features.OnlinePayments)]
[ApiController]
[Route("api/mercadopago")]
public class MercadoPagoController(
    MercadoPagoOAuthService oauth,
    TokenCipher cipher,
    IOptions<MercadoPagoOptions> options,
    IUsuarioLogadoService usuario,
    IConfiguration config) : ControllerBase
{
    [HttpPost("connect")]
    public async Task<IActionResult> Connect()
    {
        if (!cipher.IsConfigured || string.IsNullOrWhiteSpace(options.Value.ClientId))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "MERCADOPAGO_NOT_CONFIGURED",
                message = "O Mercado Pago não está configurado neste ambiente.",
            });

        var url = await oauth.CreateAuthorizationUrlAsync(usuario.ClinicaId, usuario.UserId);
        return Ok(new MercadoPagoConnectDto(url));
    }

    // A sessão é o próprio state: o callback não exige login.
    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state)
    {
        var suffix = await oauth.CompleteAsync(code, state);
        var frontendUrl = (config["FRONTEND_URL"] ?? "https://cliniqcare.com.br").TrimEnd('/');
        return Redirect($"{frontendUrl}/app/configuracoes?mp={suffix}");
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status() =>
        Ok(await oauth.GetStatusAsync(usuario.ClinicaId));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(MercadoPagoSettingsDto dto)
    {
        var result = await oauth.UpdateSettingsAsync(usuario.ClinicaId, dto);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
    }

    [HttpDelete]
    public async Task<IActionResult> Disconnect()
    {
        await oauth.DisconnectAsync(usuario.ClinicaId);
        return NoContent();
    }
}
