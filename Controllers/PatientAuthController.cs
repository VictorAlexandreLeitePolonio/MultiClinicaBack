using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MultiClinica.API.Authorization;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.PatientAuth;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/patient-auth")]
public class PatientAuthController(
    AppDbContext db,
    IConfiguration config,
    IWebHostEnvironment environment,
    IPatientTokenService tokenService,
    IPatientNotificationService notifications) : ControllerBase
{
    private const string CookieName = "patient_auth_token";
    private const int MinPasswordLength = 8;

    // ── Login ────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(PatientLoginDto dto)
    {
        var email = Normalize(dto.Email);
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.Email == email && !a.IsDeleted);

        if (account is null || string.IsNullOrEmpty(account.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(dto.Password, account.PasswordHash))
            return Unauthorized(new { message = "Email ou senha inválidos." });

        if (account.Status != PatientAccountStatus.Active)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Conta não está ativa. Verifique seu e-mail de ativação." });

        IssueCookie(account);
        return Ok(MapAccount(account));
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieName);
        return Ok(new { message = "Sessão encerrada." });
    }

    // ── Me ───────────────────────────────────────────────────────────────────

    [Authorize(AuthenticationSchemes = AuthSchemes.PatientAuth)]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var account = await CurrentAccountAsync();
        if (account is null)
            return Unauthorized(new { message = "Sessão inválida." });

        return Ok(MapAccount(account));
    }

    // ── Ativação ─────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpPost("activate")]
    public async Task<IActionResult> Activate(ActivateAccountDto dto)
    {
        if (!IsPasswordValid(dto.Password, out var error))
            return BadRequest(new { message = error });

        var token = await tokenService.ValidateAsync(dto.Token, PatientAuthTokenType.Activation);
        if (token is null)
            return BadRequest(new { message = "Token inválido ou expirado." });

        var account = token.PatientAccount;
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        account.Status = PatientAccountStatus.Active;
        account.ActivatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await tokenService.ConsumeAsync(token);

        IssueCookie(account);
        return Ok(MapAccount(account));
    }

    [AllowAnonymous]
    [HttpPost("resend-activation")]
    public async Task<IActionResult> ResendActivation(ResendActivationDto dto)
    {
        var email = Normalize(dto.Email);
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.Email == email && !a.IsDeleted);

        if (account is { Status: PatientAccountStatus.PendingActivation })
            await notifications.SendActivationInviteAsync(account);

        // Resposta genérica — não revela se o e-mail existe.
        return Ok(new { message = "Se houver uma conta pendente para este e-mail, o convite foi reenviado." });
    }

    // ── Redefinição de senha ─────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var email = Normalize(dto.Email);
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.Email == email && !a.IsDeleted);

        if (account is { Status: PatientAccountStatus.Active })
            await notifications.SendPasswordResetAsync(account);

        // Resposta genérica — não revela se o e-mail existe.
        return Ok(new { message = "Se houver uma conta para este e-mail, enviamos as instruções de redefinição." });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        if (!IsPasswordValid(dto.Password, out var error))
            return BadRequest(new { message = error });

        var token = await tokenService.ValidateAsync(dto.Token, PatientAuthTokenType.PasswordReset);
        if (token is null)
            return BadRequest(new { message = "Token inválido ou expirado." });

        var account = token.PatientAccount;
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        await db.SaveChangesAsync();
        await tokenService.ConsumeAsync(token);

        return Ok(new { message = "Senha redefinida com sucesso." });
    }

    [Authorize(AuthenticationSchemes = AuthSchemes.PatientAuth)]
    [HttpPatch("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        if (!IsPasswordValid(dto.NewPassword, out var error))
            return BadRequest(new { message = error });

        var account = await CurrentAccountAsync();
        if (account is null)
            return Unauthorized(new { message = "Sessão inválida." });

        if (string.IsNullOrEmpty(account.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, account.PasswordHash))
            return BadRequest(new { message = "Senha atual incorreta." });

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { message = "Senha alterada com sucesso." });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static bool IsPasswordValid(string password, out string error)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
        {
            error = $"A senha deve ter ao menos {MinPasswordLength} caracteres.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private async Task<PatientAccount?> CurrentAccountAsync()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var accountId))
            return null;

        return await db.PatientAccounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted);
    }

    private void IssueCookie(PatientAccount account)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Email, account.Email ?? string.Empty),
            new Claim("account_type", "patient")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:PatientAudience"] ?? "MultiClinica.Patient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        Response.Cookies.Append(CookieName, tokenString, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing"),
            SameSite = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? SameSiteMode.Lax
                : SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });
    }

    private static PatientAuthResponseDto MapAccount(PatientAccount account) => new()
    {
        Id     = account.Id,
        Name   = account.Name,
        Email  = account.Email,
        Status = account.Status
    };
}
