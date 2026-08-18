namespace MultiClinica.API.Services.Interfaces;

/// <summary>Resolve a identidade do paciente autenticado a partir do esquema PatientAuth.</summary>
public interface IPatientAccountLoggedService
{
    int PatientAccountId { get; }
    string? Email { get; }
}
