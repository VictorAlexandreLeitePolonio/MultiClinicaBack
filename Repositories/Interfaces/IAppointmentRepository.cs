using ProjetoLP.API.Models;

namespace ProjetoLP.API.Repositories.Interfaces;

public interface IAppointmentRepository
{
    /// <summary>Retorna consultas paginadas com filtros opcionais.</summary>
    Task<(List<Appointment> Items, int TotalCount)> GetPagedAsync(
        AppointmentStatus? status,
        DateOnly? date,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? patientName,
        int page,
        int pageSize);

    /// <summary>Busca uma consulta pelo Id, incluindo Patient e User.</summary>
    Task<Appointment?> GetByIdAsync(int id);

    /// <summary>Adiciona e salva uma nova consulta.</summary>
    Task<Appointment> AddAsync(Appointment appointment);

    /// <summary>Salva alterações em uma consulta já rastreada.</summary>
    Task SaveChangesAsync();

    /// <summary>Remove uma consulta já rastreada.</summary>
    Task DeleteAsync(Appointment appointment);
}
