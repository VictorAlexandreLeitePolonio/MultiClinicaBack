using System.Security.Claims;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class PatientAccountLoggedService(IHttpContextAccessor httpContextAccessor) : IPatientAccountLoggedService
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("Paciente não autenticado.");

    public int PatientAccountId => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Claim patientAccountId ausente."));

    public string? Email => User.FindFirstValue(ClaimTypes.Email);
}
