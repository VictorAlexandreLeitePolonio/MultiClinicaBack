using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class ContaReceberRepository(AppDbContext db, IUsuarioLogadoService usuario) : IContaReceberRepository
{
    private IQueryable<ContaReceber> Scoped =>
        db.ContasReceber.Where(c => c.ClinicaId == usuario.ClinicaId && !c.IsDeleted);

    public async Task<(List<ContaReceber> Items, int TotalCount)> GetPagedAsync(
        int? pacienteId,
        StatusContaReceber? status,
        int page,
        int pageSize)
    {
        var query = Scoped;
        if (pacienteId is not null)
            query = query.Where(c => c.PacienteId == pacienteId);
        if (status is not null)
            query = query.Where(c => c.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.DataVencimento)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<ContaReceber?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(c => c.Id == id);

    public Task<List<ContaReceber>> GetInadimplentesAsync() =>
        Scoped
            .Where(c => (c.Status == StatusContaReceber.Aberta || c.Status == StatusContaReceber.Parcial)
                && c.DataVencimento < DateTime.UtcNow)
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();

    public Task<bool> PatientExistsAsync(int patientId) =>
        db.Patients.AnyAsync(p => p.Id == patientId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);

    public Task<bool> CategoriaExistsAsync(int categoriaId) =>
        db.CategoriasFinanceiras.AnyAsync(c => c.Id == categoriaId && c.ClinicaId == usuario.ClinicaId && !c.IsDeleted);

    public async Task<ContaReceber> AddAsync(ContaReceber entity)
    {
        db.ContasReceber.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
