using ProjetoLP.API.Models;

namespace ProjetoLP.API.Repositories.Interfaces;

public interface IMedicalRecordRepository
{
    /// <summary>Retorna prontuários paginados com filtros opcionais.</summary>
    Task<(List<MedicalRecord> Items, int TotalCount)> GetPagedAsync(
        int? patientId,
        string? patientName,
        int? userId,
        DateOnly? createdAt,
        int page,
        int pageSize);

    /// <summary>Busca um prontuário pelo Id, incluindo User e Patient.</summary>
    Task<MedicalRecord?> GetByIdAsync(int id);

    /// <summary>Adiciona e salva um novo prontuário.</summary>
    Task<MedicalRecord> AddAsync(MedicalRecord medicalRecord);

    /// <summary>Salva alterações em um prontuário já rastreado.</summary>
    Task SaveChangesAsync();

    /// <summary>Remove um prontuário já rastreado.</summary>
    Task DeleteAsync(MedicalRecord medicalRecord);
}
