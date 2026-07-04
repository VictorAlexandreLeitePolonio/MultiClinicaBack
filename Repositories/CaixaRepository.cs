using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class CaixaRepository(AppDbContext db, IUsuarioLogadoService usuario) : ICaixaRepository
{
    private IQueryable<Caixa> Scoped =>
        db.Caixas.Where(c => c.ClinicaId == usuario.ClinicaId && !c.IsDeleted);

    public Task<Caixa?> GetAbertoAsync() =>
        Scoped.FirstOrDefaultAsync(c => c.Status == StatusCaixa.Aberto);

    public Task<Caixa?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(c => c.Id == id);

    public Task<bool> ContaFinanceiraExistsAsync(int contaFinanceiraId) =>
        db.ContasFinanceiras.AnyAsync(c => c.Id == contaFinanceiraId
            && c.ClinicaId == usuario.ClinicaId
            && !c.IsDeleted
            && c.IsActive);

    public async Task<Caixa> AddAsync(Caixa entity)
    {
        db.Caixas.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
