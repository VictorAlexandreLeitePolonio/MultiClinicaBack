using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;

namespace MultiClinica.API.Services.Interfaces;

public interface IClinicExpenseService
{
    Task<Result<PagedResult<ClinicExpenseResponseDto>>> GetPagedAsync(DateTime? startDate, DateTime? endDate, int page, int pageSize);
    Task<Result<ClinicExpenseResponseDto>> GetByIdAsync(int id);
    Task<Result<ClinicExpenseResponseDto>> CreateAsync(CreateClinicExpenseDto dto);
    Task<Result<ClinicExpenseResponseDto>> UpdateAsync(int id, UpdateClinicExpenseDto dto);
    Task<Result<bool>> DeleteAsync(int id);
}
