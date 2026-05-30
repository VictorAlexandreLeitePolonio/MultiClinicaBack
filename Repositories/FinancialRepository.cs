using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;

namespace ProjetoLP.API.Repositories;

public class FinancialRepository(AppDbContext db) : IFinancialRepository
{
    public async Task<(List<Expense> Items, int TotalCount)> GetExpensesPagedAsync(
        string? month,
        string? title,
        int page,
        int pageSize)
    {
        var query = db.Expenses.AsQueryable();

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
        => await db.Expenses.FindAsync(id);

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
        db.Expenses.Remove(expense);
        await db.SaveChangesAsync();
    }

    public async Task<decimal> GetTotalExpensesByMonthAsync(string month)
        => await db.Expenses
            .Where(e => e.ReferenceMonth == month)
            .SumAsync(e => (decimal?)e.Value) ?? 0m;

    public async Task<decimal> GetTotalIncomeByMonthAsync(string month)
        => await db.Payments
            .Where(p => p.ReferenceMonth == month && p.Status == PaymentStatus.Paid)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
}
