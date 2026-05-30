// Dependências para controller, EF Core, JWT e segurança.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjetoLP.API.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ProjetoLP.API.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;

[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    // IConfiguration permite acessar o appsettings.json (Jwt:Key, Jwt:Issuer, Jwt:Audience).
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // POST /api/auth/login — autentica o usuário e retorna um token JWT.
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        // Busca o usuário pelo email — FirstOrDefault retorna null se não encontrar.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        // BCrypt.Verify compara a senha digitada com o hash salvo no banco.
        // Retorna 401 tanto para email inexistente quanto para senha errada —
        // nunca informar qual dos dois falhou (segurança).
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized("Email ou Senha inválidos.");
        }

        // Claims são informações embutidas no token.
        // O backend usa o Role para autorizar endpoints, o frontend usa para controlar a UI.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        // Chave simétrica gerada a partir da Jwt:Key do appsettings.json.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        // Credenciais de assinatura — HmacSha256 é o algoritmo padrão para JWT.
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Monta o token com issuer, audience, claims, expiração e assinatura.
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8), // Token expira em 8 horas.
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        Response.Cookies.Append("auth_token", tokenString, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
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
                role  = user.Role.ToString()
            }
        });
    }
}
