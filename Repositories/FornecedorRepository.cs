using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class FornecedorRepository(AppDbContext db, IUsuarioLogadoService usuario) : IFornecedorRepository
{
    private IQueryable<Fornecedor> Scoped =>
        db.Fornecedores.Where(f => f.ClinicaId == usuario.ClinicaId && !f.IsDeleted);

    public async Task<(List<Fornecedor> Items, int TotalCount)> GetPagedAsync(string? nome, int page, int pageSize)
    {
        var query = Scoped;
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(f => f.Nome.Contains(nome));

        var total = await query.CountAsync();
        var items = await query.OrderBy(f => f.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public Task<Fornecedor?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(f => f.Id == id);

    public Task<bool> ExistsAsync(int id) =>
        Scoped.AnyAsync(f => f.Id == id && f.IsActive);

    public async Task<Fornecedor> AddAsync(Fornecedor entity)
    {
        db.Fornecedores.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }
}
