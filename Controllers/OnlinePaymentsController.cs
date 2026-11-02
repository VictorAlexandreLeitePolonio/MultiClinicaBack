using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.Services.MercadoPago;

namespace MultiClinica.API.Controllers;

// Ações da clínica sobre uma cobrança: enviar por e-mail, copiar o link e reembolsar.
[Authorize(Roles = "Administrador")]
[RequireFeature(Features.OnlinePayments)]
[ApiController]
[Route("api/payments")]
public class OnlinePaymentsController(OnlinePaymentService service) : ControllerBase
{
    [HttpPost("{id:int}/send")]
    public async Task<IActionResult> Send(int id, CancellationToken ct)
    {
        var result = await service.SendAsync(id, ct);
        return result.IsSuccess ? Ok(new { queued = true }) : Error(result);
    }

    [HttpGet("{id:int}/link")]
    public async Task<IActionResult> Link(int id, CancellationToken ct)
    {
        var result = await service.GetLinkAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpPost("{id:int}/refund")]
    public async Task<IActionResult> Refund(int id, CancellationToken ct)
    {
        var result = await service.RefundAsync(id, ct);
        return result.IsSuccess ? Ok(new { refunded = true }) : Error(result);
    }

    [HttpPost("{id:int}/refund-duplicate")]
    public async Task<IActionResult> RefundDuplicate(int id, CancellationToken ct)
    {
        var result = await service.RefundDuplicateAsync(id, ct);
        return result.IsSuccess ? Ok(new { refunded = true }) : Error(result);
    }

    private IActionResult Error<T>(Result<T> result)
        => result.ErrorCode == ErrorCodes.NotFound
            ? NotFound(new { code = result.ErrorCode, message = result.ErrorMessage })
            : BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
}
