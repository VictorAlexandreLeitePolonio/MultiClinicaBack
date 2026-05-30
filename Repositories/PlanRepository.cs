using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;

namespace ProjetoLP.API.Repositories;

public class PlanRepository(AppDbContext db) : IPlanRepository
{
    public async Task<(List<Plans> Items, int TotalCount)> GetPagedAsync(
        TipoPlano? tipoPlano,
        TipoSessao? tipoSessao,
        bool? isActive,
        int page,
        int pageSize)
    {
        var query = db.Plans.AsQueryable();

        if (tipoPlano.HasValue)
            query = query.Where(p => p.TipoPlano == tipoPlano.Value);

        if (tipoSessao.HasValue)
            query = query.Where(p => p.TipoSessao == tipoSessao.Value);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Plans?> GetByIdAsync(int id)
        => await db.Plans.FindAsync(id);

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        var query = db.Plans.Where(p => p.Name == name);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<bool> HasPaymentsAsync(int id)
        => await db.Payments.AnyAsync(p => p.PlanId == id);

    public async Task<Plans> AddAsync(Plans plan)
    {
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    public async Task SaveChangesAsync()
        => await db.SaveChangesAsync();

    public async Task DeleteAsync(Plans plan)
    {
        db.Plans.Remove(plan);
        await db.SaveChangesAsync();
    }
}
