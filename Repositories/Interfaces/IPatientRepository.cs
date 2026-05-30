using ProjetoLP.API.Models;

namespace ProjetoLP.API.Repositories.Interfaces;

public interface IPatientRepository
{
    /// <summary>Retorna pacientes paginados com filtros opcionais.</summary>
    Task<(List<Patient> Items, int TotalCount)> GetPagedAsync(
        string? name,
        bool? isActive,
        AppointmentStatus? appointmentStatus,
        PaymentStatus? paymentStatus,
        int page,
        int pageSize);

    /// <summary>Busca um paciente pelo Id, incluindo relacionamentos.</summary>
    Task<Patient?> GetByIdAsync(int id);

    /// <summary>Busca um paciente completo pelo Id (com todos os relacionamentos).</summary>
    Task<Patient?> GetByIdWithDetailsAsync(int id);

    /// <summary>Verifica se o email já está cadastrado.</summary>
    Task<bool> EmailExistsAsync(string? email, int? excludeId = null);

    /// <summary>Verifica se o CPF já está cadastrado.</summary>
    Task<bool> CpfExistsAsync(string? cpf, int? excludeId = null);

    /// <summary>Verifica se o paciente tem agendamentos ou pagamentos associados.</summary>
    Task<bool> HasAssociatedRecordsAsync(int id);

    /// <summary>Adiciona e salva um novo paciente.</summary>
    Task<Patient> AddAsync(Patient patient);

    /// <summary>Salva alterações em um paciente já rastreado.</summary>
    Task SaveChangesAsync();

    /// <summary>Remove um paciente já rastreado.</summary>
    Task DeleteAsync(Patient patient);
}
