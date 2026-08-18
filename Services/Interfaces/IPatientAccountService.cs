using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

/// <summary>
/// Centraliza a normalização de e-mail/CPF e a resolução da identidade global
/// do paciente (<see cref="PatientAccount"/>).
/// </summary>
public interface IPatientAccountService
{
    string? NormalizeEmail(string? email);
    string? NormalizeCpf(string? cpf);

    /// <summary>Busca a conta global pelo e-mail já normalizado.</summary>
    Task<PatientAccount?> FindByEmailAsync(string? normalizedEmail);

    /// <summary>Verifica se o CPF já pertence a alguma conta global.</summary>
    Task<bool> CpfExistsAsync(string? normalizedCpf);

    /// <summary>
    /// Cria (em memória) uma conta global pendente de ativação. A persistência
    /// ocorre junto ao <see cref="Patient"/> pai numa única transação.
    /// </summary>
    PatientAccount CreatePending(string? name, string? normalizedEmail, string? normalizedCpf, string? phone, int? createdByUserId);
}
