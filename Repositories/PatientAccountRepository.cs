using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;

namespace MultiClinica.API.Repositories;

public class PatientAccountRepository(AppDbContext db) : IPatientAccountRepository
{
    public async Task<PatientAccount?> GetByEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await db.PatientAccounts
            .FirstOrDefaultAsync(a => a.Email == email && !a.IsDeleted);
    }

    public async Task<bool> CpfExistsAsync(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        return await db.PatientAccounts
            .AnyAsync(a => a.CPF == cpf && !a.IsDeleted);
    }
}
