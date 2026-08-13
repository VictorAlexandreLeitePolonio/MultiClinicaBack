using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IAuditoriaFinanceiraRepository
{
    Task AddAsync(AuditoriaFinanceira entity);
}
