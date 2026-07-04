using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface ICaixaRepository
{
    Task<Caixa?> GetAbertoAsync();
    Task<Caixa?> GetByIdAsync(int id);
    Task<bool> ContaFinanceiraExistsAsync(int contaFinanceiraId);
    Task<Caixa> AddAsync(Caixa entity);
    Task SaveChangesAsync();
}
