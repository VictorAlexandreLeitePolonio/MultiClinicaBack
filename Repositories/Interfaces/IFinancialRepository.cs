using ProjetoLP.API.DTOs.Financial;
using ProjetoLP.API.Models;

namespace ProjetoLP.API.Repositories.Interfaces;

public interface IFinancialRepository
{
    /// <summary>Retorna gastos paginados com filtros opcionais.</summary>
    Task<(List<Expense> Items, int TotalCount)> GetExpensesPagedAsync(
        string? month,
        string? title,
        int page,
        int pageSize);

    /// <summary>Busca um gasto pelo Id.</summary>
    Task<Expense?> GetExpenseByIdAsync(int id);

    /// <summary>Adiciona e salva um novo gasto.</summary>
    Task<Expense> AddExpenseAsync(Expense expense);

    /// <summary>Salva alterações em um gasto já rastreado.</summary>
    Task SaveChangesAsync();

    /// <summary>Remove um gasto já rastreado.</summary>
    Task DeleteExpenseAsync(Expense expense);

    /// <summary>Calcula o total de despesas de um mês.</summary>
    Task<decimal> GetTotalExpensesByMonthAsync(string month);

    /// <summary>Calcula o total de receitas pagas de um mês.</summary>
    Task<decimal> GetTotalIncomeByMonthAsync(string month);
}
