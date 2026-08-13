using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;

namespace MultiClinica.API.Repositories;

public class AuditoriaFinanceiraRepository(AppDbContext db) : IAuditoriaFinanceiraRepository
{
    public async Task AddAsync(AuditoriaFinanceira entity)
    {
        db.AuditoriasFinanceiras.Add(entity);
        await db.SaveChangesAsync();
    }
}
