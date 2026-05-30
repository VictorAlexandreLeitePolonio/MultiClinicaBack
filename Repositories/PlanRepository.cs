using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class PlanRepository(AppDbContext db, IUsuarioLogadoService usuario) : IPlanRepository
{
    public async Task<(List<Plans> Items, int TotalCount)> GetPagedAsync(
        TipoPlano? tipoPlano,
        TipoSessao? tipoSessao,
        bool? isActive,
        int page,
        int pageSize)
    {
        var query = db.Plans.Where(p => p.ClinicaId == usuario.ClinicaId && !p.IsDeleted).AsQueryable();

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
        => await db.Plans.FirstOrDefaultAsync(p => p.Id == id && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        var query = db.Plans.Where(p => p.Name == name && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<bool> HasPaymentsAsync(int id)
        => await db.Payments.AnyAsync(p => p.PlanId == id && p.ClinicaId == usuario.ClinicaId);

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
        plan.IsDeleted = true;
        plan.DeletedAt = DateTime.UtcNow;
        plan.DeletedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
    }
}
