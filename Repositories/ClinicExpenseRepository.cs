using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class ClinicExpenseRepository(AppDbContext db, IUsuarioLogadoService usuario) : IClinicExpenseRepository
{
    private IQueryable<ClinicExpense> Scoped =>
        db.ClinicExpenses.Where(e => e.ClinicaId == usuario.ClinicaId && !e.IsDeleted);

    public async Task<(List<ClinicExpense> Items, int TotalCount)> GetPagedAsync(DateTime? startDate, DateTime? endDate, int page, int pageSize)
    {
        var query = Scoped;
        if (startDate is not null)
            query = query.Where(e => e.Date >= startDate);
        if (endDate is not null)
            query = query.Where(e => e.Date <= endDate);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public Task<ClinicExpense?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(e => e.Id == id);

    public async Task<ClinicExpense> AddAsync(ClinicExpense entity)
    {
        db.ClinicExpenses.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
