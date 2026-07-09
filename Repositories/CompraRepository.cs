using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class CompraRepository(AppDbContext db, IUsuarioLogadoService usuario)
{
    private IQueryable<Compra> Scoped =>
        db.Compras
            .Where(c => c.ClinicaId == usuario.ClinicaId && !c.IsDeleted)
            .Include(c => c.Itens);

    public async Task<(List<Compra> Items, int TotalCount)> GetPagedAsync(
        int? fornecedorId,
        StatusCompra? status,
        int page,
        int pageSize)
    {
        var query = Scoped;
        if (fornecedorId is not null)
            query = query.Where(c => c.FornecedorId == fornecedorId);
        if (status is not null)
            query = query.Where(c => c.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.DataCompra)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<Compra?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(c => c.Id == id);

    public Task<bool> FornecedorExistsAsync(int fornecedorId) =>
        db.Fornecedores.AnyAsync(f => f.Id == fornecedorId
            && f.ClinicaId == usuario.ClinicaId
            && !f.IsDeleted
            && f.IsActive);

    public async Task<Compra> AddAsync(Compra entity)
    {
        db.Compras.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
