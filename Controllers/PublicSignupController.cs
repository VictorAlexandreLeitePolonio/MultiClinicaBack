using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MultiClinica.API.DTOs.Signup;
using MultiClinica.API.Services;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/public/signup")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class PublicSignupController(SignupService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Signup(SignupRequestDto dto)
    {
        await service.SignupAsync(dto);
        return Accepted();
    }

    [HttpPost("resend")]
    public async Task<IActionResult> Resend(ResendSignupDto dto)
    {
        await service.ResendAsync(dto);
        return Accepted();
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(ConfirmSignupDto dto)
    {
        var result = await service.ConfirmAsync(dto.Token);
        return result.IsSuccess
            ? Ok(new { email = result.Value })
            : BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
    }
}
