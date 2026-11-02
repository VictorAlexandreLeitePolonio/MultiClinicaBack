using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Agent;

// Preenchido pelo worker do agente antes de chamar os services; fora do HTTP não há claims.
public sealed class AgentIdentity
{
    public int? ClinicaId { get; private set; }
    public int? UserId { get; private set; }
    public UserRole? Role { get; private set; }

    public void Set(int clinicaId, int userId, UserRole role)
    {
        ClinicaId = clinicaId;
        UserId = userId;
        Role = role;
    }
}
