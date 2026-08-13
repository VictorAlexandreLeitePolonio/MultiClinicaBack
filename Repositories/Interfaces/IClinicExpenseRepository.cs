using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IClinicExpenseRepository
{
    Task<(List<ClinicExpense> Items, int TotalCount)> GetPagedAsync(DateTime? startDate, DateTime? endDate, int page, int pageSize);
    Task<ClinicExpense?> GetByIdAsync(int id);
    Task<ClinicExpense> AddAsync(ClinicExpense entity);
    Task SaveChangesAsync();
}
