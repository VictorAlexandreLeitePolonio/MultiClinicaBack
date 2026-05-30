using System.Security.Claims;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class UsuarioLogadoService(IHttpContextAccessor httpContextAccessor) : IUsuarioLogadoService
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("Usuário não autenticado.");

    public int UserId => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Claim userId ausente."));

    public int ClinicaId => int.Parse(
        User.FindFirstValue("clinicaId")
            ?? throw new InvalidOperationException("Claim clinicaId ausente."));

    public UserRole Role => Enum.Parse<UserRole>(
        User.FindFirstValue(ClaimTypes.Role)
            ?? throw new InvalidOperationException("Claim role ausente."));

    public bool IsSuperAdmin => Role == UserRole.SuperAdmin;
}
