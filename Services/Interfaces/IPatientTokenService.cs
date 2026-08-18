using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

public interface IPatientTokenService
{
    /// <summary>
    /// Gera um token de uso único, persiste apenas o hash e retorna o token puro
    /// (para compor o link enviado por e-mail).
    /// </summary>
    Task<string> IssueAsync(int patientAccountId, PatientAuthTokenType type, TimeSpan ttl);

    /// <summary>Busca um token válido (não consumido e não expirado) pelo valor puro.</summary>
    Task<PatientAuthToken?> ValidateAsync(string rawToken, PatientAuthTokenType type);

    /// <summary>Marca o token como consumido (uso único).</summary>
    Task ConsumeAsync(PatientAuthToken token);
}
