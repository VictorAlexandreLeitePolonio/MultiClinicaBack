using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class FinancialRepository(AppDbContext db, IUsuarioLogadoService usuario) : IFinancialRepository
{
    public async Task<(List<Expense> Items, int TotalCount)> GetExpensesPagedAsync(
        string? month,
        string? title,
        int page,
        int pageSize)
    {
        var query = db.Expenses.Where(e => e.ClinicaId == usuario.ClinicaId && !e.IsDeleted).AsQueryable();

        if (!string.IsNullOrEmpty(month))
            query = query.Where(e => e.ReferenceMonth == month);

        if (!string.IsNullOrEmpty(title))
            query = query.Where(e => e.Title.Contains(title));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.PaymentDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Expense?> GetExpenseByIdAsync(int id)
        => await db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.ClinicaId == usuario.ClinicaId && !e.IsDeleted);

    public async Task<Expense> AddExpenseAsync(Expense expense)
    {
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
        return expense;
    }

    public async Task SaveChangesAsync()
        => await db.SaveChangesAsync();

    public async Task DeleteExpenseAsync(Expense expense)
    {
        expense.IsDeleted = true;
        expense.DeletedAt = DateTime.UtcNow;
        expense.DeletedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
    }

    public async Task<decimal> GetTotalExpensesByMonthAsync(string month)
        => await db.Expenses
            .Where(e => e.ReferenceMonth == month)
            .Where(e => e.ClinicaId == usuario.ClinicaId && !e.IsDeleted)
            .SumAsync(e => (decimal?)e.Value) ?? 0m;

    public async Task<decimal> GetTotalIncomeByMonthAsync(string month)
        => await db.Payments
            .Where(p => p.ReferenceMonth == month && p.Status == PaymentStatus.Paid)
            .Where(p => p.ClinicaId == usuario.ClinicaId && !p.IsDeleted)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
}
