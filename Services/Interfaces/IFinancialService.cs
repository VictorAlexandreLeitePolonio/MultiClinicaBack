using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Financial;

namespace ProjetoLP.API.Services.Interfaces;

public interface IFinancialService
{
    Task<Result<PagedResult<ExpenseResponseDto>>> GetExpensesPagedAsync(
        string? month,
        string? title,
        int page,
        int pageSize);

    Task<Result<ExpenseResponseDto>> GetExpenseByIdAsync(int id);

    Task<Result<ExpenseResponseDto>> CreateExpenseAsync(CreateExpenseDto dto);

    Task<Result<ExpenseResponseDto>> UpdateExpenseAsync(int id, UpdateExpenseDto dto);

    Task<Result<bool>> DeleteExpenseAsync(int id);

    Task<Result<FinancialBalanceDto>> GetBalanceAsync(string month);

    Task<Result<List<FinancialBalanceDto>>> GetBalanceHistoryAsync(int months);
}
