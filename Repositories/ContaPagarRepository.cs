using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class ContaPagarRepository(AppDbContext db, IUsuarioLogadoService usuario) : IContaPagarRepository
{
    private IQueryable<ContaPagar> Scoped =>
        db.ContasPagar.Where(c => c.ClinicaId == usuario.ClinicaId && !c.IsDeleted);

    public async Task<(List<ContaPagar> Items, int TotalCount)> GetPagedAsync(int? fornecedorId, StatusContaPagar? status, int page, int pageSize)
    {
        var query = Scoped;
        if (fornecedorId is not null)
            query = query.Where(c => c.FornecedorId == fornecedorId);
        if (status is not null)
            query = query.Where(c => c.Status == status);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(c => c.DataVencimento)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public Task<ContaPagar?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(c => c.Id == id);

    public Task<bool> FornecedorExistsAsync(int fornecedorId) =>
        db.Fornecedores.AnyAsync(f => f.Id == fornecedorId
            && f.ClinicaId == usuario.ClinicaId
            && !f.IsDeleted
            && f.IsActive);

    public Task<bool> CategoriaExistsAsync(int categoriaId) =>
        db.CategoriasFinanceiras.AnyAsync(c => c.Id == categoriaId
            && c.ClinicaId == usuario.ClinicaId
            && !c.IsDeleted);

    public async Task<ContaPagar> AddAsync(ContaPagar entity)
    {
        db.ContasPagar.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
