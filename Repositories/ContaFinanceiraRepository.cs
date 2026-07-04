using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class ContaFinanceiraRepository(AppDbContext db, IUsuarioLogadoService usuario) : IContaFinanceiraRepository
{
    private IQueryable<ContaFinanceira> Scoped =>
        db.ContasFinanceiras.Where(c => c.ClinicaId == usuario.ClinicaId && !c.IsDeleted);

    public async Task<(List<ContaFinanceira> Items, int TotalCount)> GetPagedAsync(string? nome, int page, int pageSize)
    {
        var query = Scoped;
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(c => c.Nome.Contains(nome));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<ContaFinanceira?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(c => c.Id == id);

    public Task<bool> ExistsActiveByNameAsync(string nome, int? excludeId) =>
        Scoped
            .Where(c => c.IsActive && c.Nome == nome)
            .Where(c => excludeId == null || c.Id != excludeId)
            .AnyAsync();

    public async Task<ContaFinanceira> AddAsync(ContaFinanceira entity)
    {
        db.ContasFinanceiras.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
