using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IContaReceberRepository
{
    Task<(List<ContaReceber> Items, int TotalCount)> GetPagedAsync(
        int? pacienteId,
        StatusContaReceber? status,
        int page,
        int pageSize);
    Task<ContaReceber?> GetByIdAsync(int id);
    Task<List<ContaReceber>> GetInadimplentesAsync();
    Task<bool> PatientExistsAsync(int patientId);
    Task<bool> CategoriaExistsAsync(int categoriaId);
    Task<ContaReceber> AddAsync(ContaReceber entity);
    Task SaveChangesAsync();
}
