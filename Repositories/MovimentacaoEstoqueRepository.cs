using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class MovimentacaoEstoqueRepository(AppDbContext db, IUsuarioLogadoService usuario)
{
    private IQueryable<MovimentacaoEstoque> Scoped =>
        db.MovimentacoesEstoque.Where(m => m.ClinicaId == usuario.ClinicaId);

    public async Task<(List<MovimentacaoEstoque> Items, int TotalCount)> GetPagedAsync(
        int? produtoId,
        TipoMovimentacaoEstoque? tipo,
        int page,
        int pageSize)
    {
        var query = Scoped;
        if (produtoId is not null)
            query = query.Where(m => m.ProdutoId == produtoId);
        if (tipo is not null)
            query = query.Where(m => m.Tipo == tipo);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<MovimentacaoEstoque?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(m => m.Id == id);

    public Task<Produto?> GetProdutoAsync(int produtoId) =>
        db.Produtos.FirstOrDefaultAsync(p => p.Id == produtoId
            && p.ClinicaId == usuario.ClinicaId
            && !p.IsDeleted
            && p.IsActive);

    public async Task<MovimentacaoEstoque> AddAsync(MovimentacaoEstoque entity)
    {
        db.MovimentacoesEstoque.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
