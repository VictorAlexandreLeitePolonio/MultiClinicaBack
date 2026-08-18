using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IPatientAccountRepository
{
    /// <summary>Busca a conta global pelo e-mail normalizado (escopo global, sem clínica).</summary>
    Task<PatientAccount?> GetByEmailAsync(string? email);

    /// <summary>Verifica se já existe conta global com o CPF informado.</summary>
    Task<bool> CpfExistsAsync(string? cpf);
}
