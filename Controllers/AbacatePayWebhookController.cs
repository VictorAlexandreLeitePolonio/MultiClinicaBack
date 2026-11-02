using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Services.Billing;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/billing/webhooks")]
[AllowAnonymous]
public class AbacatePayWebhookController(BillingService billing) : ControllerBase
{
    [HttpPost("abacatepay")]
    public async Task<IActionResult> Receive([FromQuery] string? webhookSecret)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Webhook-Signature"].FirstOrDefault();

        var result = await billing.ProcessWebhookAsync(webhookSecret, rawBody, signature);
        return result.IsSuccess
            ? Ok()
            : Unauthorized(new { message = result.ErrorMessage });
    }
}
