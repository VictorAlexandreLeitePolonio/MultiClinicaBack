using System.Text.RegularExpressions;
using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Financial;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;
using ProjetoLP.API.Services.Interfaces;

namespace ProjetoLP.API.Services;

public partial class FinancialService(IFinancialRepository repository) : IFinancialService
{
    [GeneratedRegex(@"^\d{4}-\d{2}$")]
    private static partial Regex ReferenceMonthRegex();

    // ── Expenses ─────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<ExpenseResponseDto>>> GetExpensesPagedAsync(
        string? month, string? title, int page, int pageSize)
    {
        // Valida formato do mês se informado
        if (!string.IsNullOrEmpty(month) && !ReferenceMonthRegex().IsMatch(month))
            return Result<PagedResult<ExpenseResponseDto>>.Fail(
                ErrorCodes.InvalidFormat, "O formato do mês deve ser 'YYYY-MM'.");

        var (items, total) = await repository.GetExpensesPagedAsync(month, title, page, pageSize);

        var data = items.Select(e => new ExpenseResponseDto
        {
            Id             = e.Id,
            Title          = e.Title,
            Value          = e.Value,
            PaymentDate    = e.PaymentDate,
            Description    = e.Description,
            ReferenceMonth = e.ReferenceMonth,
            CreatedAt      = e.CreatedAt,
        });

        return Result<PagedResult<ExpenseResponseDto>>.Ok(new PagedResult<ExpenseResponseDto>
        {
            Data       = data,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        });
    }

    public async Task<Result<ExpenseResponseDto>> GetExpenseByIdAsync(int id)
    {
        var expense = await repository.GetExpenseByIdAsync(id);
        if (expense is null)
            return Result<ExpenseResponseDto>.Fail(ErrorCodes.NotFound, "Gasto não encontrado.");

        return Result<ExpenseResponseDto>.Ok(new ExpenseResponseDto
        {
            Id             = expense.Id,
            Title          = expense.Title,
            Value          = expense.Value,
            PaymentDate    = expense.PaymentDate,
            Description    = expense.Description,
            ReferenceMonth = expense.ReferenceMonth,
            CreatedAt      = expense.CreatedAt,
        });
    }

    public async Task<Result<ExpenseResponseDto>> CreateExpenseAsync(CreateExpenseDto dto)
    {
        // Validações
        if (dto.Value <= 0)
            return Result<ExpenseResponseDto>.Fail(
                ErrorCodes.InvalidValue, "O valor do gasto deve ser maior que zero.");

        if (!ReferenceMonthRegex().IsMatch(dto.ReferenceMonth))
            return Result<ExpenseResponseDto>.Fail(
                ErrorCodes.InvalidFormat, "O formato do mês de referência deve ser 'YYYY-MM'.");

        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<ExpenseResponseDto>.Fail(
                ErrorCodes.EmptyField, "O título do gasto é obrigatório.");

        var expense = new Expense
        {
            Title          = dto.Title,
            Value          = dto.Value,
            PaymentDate    = dto.PaymentDate,
            Description    = dto.Description,
            ReferenceMonth = dto.ReferenceMonth,
        };

        await repository.AddExpenseAsync(expense);

        return Result<ExpenseResponseDto>.Ok(new ExpenseResponseDto
        {
            Id             = expense.Id,
            Title          = expense.Title,
            Value          = expense.Value,
            PaymentDate    = expense.PaymentDate,
            Description    = expense.Description,
            ReferenceMonth = expense.ReferenceMonth,
            CreatedAt      = expense.CreatedAt,
        });
    }

    public async Task<Result<ExpenseResponseDto>> UpdateExpenseAsync(int id, UpdateExpenseDto dto)
    {
        // Validações
        if (dto.Value <= 0)
            return Result<ExpenseResponseDto>.Fail(
                ErrorCodes.InvalidValue, "O valor do gasto deve ser maior que zero.");

        if (!ReferenceMonthRegex().IsMatch(dto.ReferenceMonth))
            return Result<ExpenseResponseDto>.Fail(
                ErrorCodes.InvalidFormat, "O formato do mês de referência deve ser 'YYYY-MM'.");

        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<ExpenseResponseDto>.Fail(
                ErrorCodes.EmptyField, "O título do gasto é obrigatório.");

        var expense = await repository.GetExpenseByIdAsync(id);
        if (expense is null)
            return Result<ExpenseResponseDto>.Fail(ErrorCodes.NotFound, "Gasto não encontrado.");

        expense.Title          = dto.Title;
        expense.Value          = dto.Value;
        expense.PaymentDate    = dto.PaymentDate;
        expense.Description    = dto.Description;
        expense.ReferenceMonth = dto.ReferenceMonth;

        await repository.SaveChangesAsync();

        return Result<ExpenseResponseDto>.Ok(new ExpenseResponseDto
        {
            Id             = expense.Id,
            Title          = expense.Title,
            Value          = expense.Value,
            PaymentDate    = expense.PaymentDate,
            Description    = expense.Description,
            ReferenceMonth = expense.ReferenceMonth,
            CreatedAt      = expense.CreatedAt,
        });
    }

    public async Task<Result<bool>> DeleteExpenseAsync(int id)
    {
        var expense = await repository.GetExpenseByIdAsync(id);
        if (expense is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Gasto não encontrado.");

        await repository.DeleteExpenseAsync(expense);
        return Result<bool>.Ok(true);
    }

    // ── Balance ───────────────────────────────────────────────────────────────

    public async Task<Result<FinancialBalanceDto>> GetBalanceAsync(string month)
    {
        if (!ReferenceMonthRegex().IsMatch(month))
            return Result<FinancialBalanceDto>.Fail(
                ErrorCodes.InvalidFormat, "O formato do mês deve ser 'YYYY-MM'. Exemplo: 2026-03");

        var totalExpenses = await repository.GetTotalExpensesByMonthAsync(month);
        var totalIncome = await repository.GetTotalIncomeByMonthAsync(month);

        return Result<FinancialBalanceDto>.Ok(new FinancialBalanceDto
        {
            ReferenceMonth = month,
            TotalExpenses  = totalExpenses,
            TotalIncome    = totalIncome,
            NetBalance     = totalIncome - totalExpenses,
        });
    }

    public async Task<Result<List<FinancialBalanceDto>>> GetBalanceHistoryAsync(int months)
    {
        // Limita o período máximo a 24 meses
        if (months < 1 || months > 24)
            return Result<List<FinancialBalanceDto>>.Fail(
                ErrorCodes.InvalidValue, "O período deve ser entre 1 e 24 meses.");

        var result = new List<FinancialBalanceDto>();
        var now = DateTime.UtcNow;

        for (int i = months - 1; i >= 0; i--)
        {
            var date  = now.AddMonths(-i);
            var month = date.ToString("yyyy-MM");

            var totalExpenses = await repository.GetTotalExpensesByMonthAsync(month);
            var totalIncome = await repository.GetTotalIncomeByMonthAsync(month);

            result.Add(new FinancialBalanceDto
            {
                ReferenceMonth = month,
                TotalExpenses  = totalExpenses,
                TotalIncome    = totalIncome,
                NetBalance     = totalIncome - totalExpenses,
            });
        }

        return Result<List<FinancialBalanceDto>>.Ok(result);
    }
}
