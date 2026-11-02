using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Billing;
using MultiClinica.API.Services.Billing;

namespace MultiClinica.API.Controllers;

[ApiController]
[Authorize]
[Route("api/billing")]
public class BillingController(BillingService billing) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetStatus() => Map(await billing.GetStatusAsync());

    [Authorize(Roles = "Administrador")]
    [HttpPost("subscription/checkout")]
    public async Task<IActionResult> CreateSubscriptionCheckout(SubscriptionCheckoutRequest dto) =>
        Map(await billing.CreateSubscriptionCheckoutAsync(dto));

    [Authorize(Roles = "Administrador")]
    [HttpPost("subscription/change")]
    public async Task<IActionResult> ChangeSubscription(SubscriptionCheckoutRequest dto) =>
        MapEmpty(await billing.ChangeSubscriptionAsync(dto));

    [Authorize(Roles = "Administrador")]
    [HttpPost("subscription/cancel")]
    public async Task<IActionResult> CancelSubscription() =>
        MapEmpty(await billing.CancelSubscriptionAsync());

    [Authorize(Roles = "Administrador")]
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase(PurchaseRequest dto) => Map(await billing.PurchaseAsync(dto));

    private IActionResult Map<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : Error(result);

    private IActionResult MapEmpty(Result<bool> result) =>
        result.IsSuccess ? NoContent() : Error(result);

    private IActionResult Error<T>(Result<T> result) => result.ErrorCode switch
    {
        ErrorCodes.NotFound => NotFound(new { message = result.ErrorMessage }),
        ErrorCodes.CannotModify => Conflict(new { message = result.ErrorMessage }),
        _ => BadRequest(new { message = result.ErrorMessage }),
    };
}
