using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface ICaixaRepository
{
    Task<Caixa?> GetAbertoAsync();
    Task<(List<Caixa> Items, int TotalCount)> GetPagedAsync(StatusCaixa? status, int page, int pageSize);
    Task<Caixa?> GetByIdAsync(int id);
    Task<bool> ContaFinanceiraExistsAsync(int contaFinanceiraId);
    Task<Caixa> AddAsync(Caixa entity);
    Task SaveChangesAsync();
}
