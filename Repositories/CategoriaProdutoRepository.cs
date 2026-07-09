using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class CategoriaProdutoRepository(AppDbContext db, IUsuarioLogadoService usuario) : ICategoriaProdutoRepository
{
    private IQueryable<CategoriaProduto> Scoped =>
        db.CategoriasProduto.Where(c => c.ClinicaId == usuario.ClinicaId && !c.IsDeleted);

    public async Task<(List<CategoriaProduto> Items, int TotalCount)> GetPagedAsync(string? nome, int page, int pageSize)
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

    public Task<CategoriaProduto?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(c => c.Id == id);

    public Task<bool> ExistsActiveByNameAsync(string nome, int? excludeId) =>
        Scoped
            .Where(c => c.IsActive && c.Nome == nome)
            .Where(c => excludeId == null || c.Id != excludeId)
            .AnyAsync();

    public Task<bool> ExistsActiveAsync(int id) =>
        Scoped.AnyAsync(c => c.Id == id && c.IsActive);

    public async Task<CategoriaProduto> AddAsync(CategoriaProduto entity)
    {
        db.CategoriasProduto.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
