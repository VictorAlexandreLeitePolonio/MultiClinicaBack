using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class ProdutoRepository(AppDbContext db, IUsuarioLogadoService usuario) : IProdutoRepository
{
    private IQueryable<Produto> Scoped =>
        db.Produtos.Where(p => p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);

    public async Task<(List<Produto> Items, int TotalCount)> GetPagedAsync(
        string? nome, int? categoriaProdutoId, bool? ativo, int page, int pageSize)
    {
        var query = Scoped;
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(p => p.Nome.Contains(nome));
        if (categoriaProdutoId is not null)
            query = query.Where(p => p.CategoriaProdutoId == categoriaProdutoId);
        if (ativo is not null)
            query = query.Where(p => p.IsActive == ativo);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<Produto?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> CategoriaExistsAsync(int categoriaProdutoId) =>
        db.CategoriasProduto.AnyAsync(c => c.Id == categoriaProdutoId
            && c.ClinicaId == usuario.ClinicaId
            && !c.IsDeleted
            && c.IsActive);

    public async Task<Produto> AddAsync(Produto entity)
    {
        db.Produtos.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
