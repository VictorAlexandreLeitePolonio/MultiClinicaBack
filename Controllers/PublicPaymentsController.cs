using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Payment;
using MultiClinica.API.Services.MercadoPago;

namespace MultiClinica.API.Controllers;

// Página pública de pagamento: o token do link é a identidade da cobrança.
[ApiController]
[Route("api/public/payments")]
[AllowAnonymous]
public class PublicPaymentsController(OnlinePaymentService service) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token, CancellationToken ct)
    {
        var result = await service.GetPublicAsync(token, ct);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpPost("{token}/pay")]
    [EnableRateLimiting("public-pay")]
    public async Task<IActionResult> Pay(string token, PublicPayRequest dto, CancellationToken ct)
    {
        var result = await service.PayAsync(token, dto, ct);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    private IActionResult Error<T>(Result<T> result)
        => result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { code = result.ErrorCode, message = result.ErrorMessage })
            : BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
}
