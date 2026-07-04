using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class CategoriaFinanceiraRepository(AppDbContext db, IUsuarioLogadoService usuario) : ICategoriaFinanceiraRepository
{
    private IQueryable<CategoriaFinanceira> Scoped =>
        db.CategoriasFinanceiras.Where(c => c.ClinicaId == usuario.ClinicaId && !c.IsDeleted);

    public async Task<(List<CategoriaFinanceira> Items, int TotalCount)> GetPagedAsync(
        string? nome,
        TipoCategoriaFinanceira? tipo,
        int page,
        int pageSize)
    {
        var query = Scoped;
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(c => c.Nome.Contains(nome));
        if (tipo is not null)
            query = query.Where(c => c.Tipo == tipo);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<CategoriaFinanceira?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(c => c.Id == id);

    public Task<bool> ExistsActiveByNameAndTipoAsync(string nome, TipoCategoriaFinanceira tipo, int? excludeId) =>
        Scoped
            .Where(c => c.IsActive && c.Nome == nome && c.Tipo == tipo)
            .Where(c => excludeId == null || c.Id != excludeId)
            .AnyAsync();

    public async Task<CategoriaFinanceira> AddAsync(CategoriaFinanceira entity)
    {
        db.CategoriasFinanceiras.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
