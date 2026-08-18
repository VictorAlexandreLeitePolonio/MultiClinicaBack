using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class PatientAccountService(IPatientAccountRepository repository) : IPatientAccountService
{
    public string? NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    public string? NormalizeCpf(string? cpf)
        => string.IsNullOrWhiteSpace(cpf) ? null : new string(cpf.Where(char.IsDigit).ToArray());

    public Task<PatientAccount?> FindByEmailAsync(string? normalizedEmail)
        => repository.GetByEmailAsync(normalizedEmail);

    public Task<bool> CpfExistsAsync(string? normalizedCpf)
        => repository.CpfExistsAsync(normalizedCpf);

    public PatientAccount CreatePending(string? name, string? normalizedEmail, string? normalizedCpf, string? phone, int? createdByUserId)
        => new()
        {
            Name            = name,
            Email           = normalizedEmail,
            CPF             = normalizedCpf,
            Phone           = phone,
            Status          = PatientAccountStatus.PendingActivation,
            CreatedByUserId = createdByUserId,
        };
}
