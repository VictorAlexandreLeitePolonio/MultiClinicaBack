using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

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
