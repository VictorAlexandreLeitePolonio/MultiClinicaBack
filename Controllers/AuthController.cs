// Dependências para controller, EF Core, JWT e segurança.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MultiClinica.API.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MultiClinica.API.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using MultiClinica.API.Models;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, IConfiguration config, IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await db.Users
            .Include(u => u.Clinica)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Email ou senha inválidos." });

        if (!user.IsActive || user.IsDeleted)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Usuário inativo." });

        if (!user.Clinica.IsActive || user.Clinica.IsDeleted)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Clínica inativa." });

        if (user.Clinica.IsBlockedByBilling && user.Role != UserRole.SuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Clínica bloqueada por pendência financeira." });

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("clinicaId", user.ClinicaId.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        Response.Cookies.Append("auth_token", tokenString, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing"),
            SameSite = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? SameSiteMode.Lax
                : SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        return Ok(new
        {
            message = "Login realizado com sucesso.",
            user = new
            {
                id    = user.Id,
                name  = user.Name,
                email = user.Email,
                role  = user.Role.ToString(),
                clinicaId = user.ClinicaId,
                clinicaNome = user.Clinica.Nome
            }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users
            .Include(u => u.Clinica)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

        if (user is null)
            return Unauthorized(new { message = "Sessão inválida." });

        if (!user.IsActive)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Usuário inativo." });

        if (!user.Clinica.IsActive)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Clínica inativa." });

        if (user.Clinica.IsBlockedByBilling && user.Role != UserRole.SuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Clínica bloqueada por pendência financeira." });

        return Ok(new
        {
            id = user.Id,
            name = user.Name,
            email = user.Email,
            role = user.Role.ToString(),
            clinicaId = user.ClinicaId,
            clinicaNome = user.Clinica.Nome
        });
    }
}
