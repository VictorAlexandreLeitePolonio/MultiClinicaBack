using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

public interface IUsuarioLogadoService
{
    int UserId { get; }
    int ClinicaId { get; }
    UserRole Role { get; }
    bool IsSuperAdmin { get; }
}
