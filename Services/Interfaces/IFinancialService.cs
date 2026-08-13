using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IFinancialService
{
    Task<Result<FinancialBalanceDto>> GetBalanceAsync(string month);

    Task<Result<List<FinancialBalanceDto>>> GetBalanceHistoryAsync(int months);
}
