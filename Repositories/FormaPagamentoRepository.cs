using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class FormaPagamentoRepository(AppDbContext db, IUsuarioLogadoService usuario) : IFormaPagamentoRepository
{
    private IQueryable<FormaPagamento> Scoped =>
        db.FormasPagamento.Where(f => f.ClinicaId == usuario.ClinicaId && !f.IsDeleted);

    public async Task<(List<FormaPagamento> Items, int TotalCount)> GetPagedAsync(string? nome, int page, int pageSize)
    {
        var query = Scoped;
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(f => f.Nome.Contains(nome));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(f => f.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<FormaPagamento?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(f => f.Id == id);

    public Task<bool> ExistsActiveByNameAsync(string nome, int? excludeId) =>
        Scoped
            .Where(f => f.IsActive && f.Nome == nome)
            .Where(f => excludeId == null || f.Id != excludeId)
            .AnyAsync();

    public async Task<FormaPagamento> AddAsync(FormaPagamento entity)
    {
        db.FormasPagamento.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
