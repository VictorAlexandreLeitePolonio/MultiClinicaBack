using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class AuditoriaFinanceiraRepository(AppDbContext db, IUsuarioLogadoService usuario) : IAuditoriaFinanceiraRepository
{
    public async Task AddAsync(AuditoriaFinanceira entity)
    {
        db.AuditoriasFinanceiras.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task<(List<AuditoriaFinanceira> Items, int TotalCount)> GetPagedAsync(
        string? modulo,
        string? entidade,
        int? usuarioId,
        DateTime? de,
        DateTime? ate,
        int page,
        int pageSize)
    {
        var query = db.AuditoriasFinanceiras.Where(a => a.ClinicaId == usuario.ClinicaId);

        if (!string.IsNullOrWhiteSpace(modulo))
            query = query.Where(a => a.Modulo == modulo);
        if (!string.IsNullOrWhiteSpace(entidade))
            query = query.Where(a => a.Entidade == entidade);
        if (usuarioId is not null)
            query = query.Where(a => a.UsuarioId == usuarioId);
        if (de is not null)
            query = query.Where(a => a.DataAcao >= de);
        if (ate is not null)
            query = query.Where(a => a.DataAcao <= ate);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.DataAcao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
