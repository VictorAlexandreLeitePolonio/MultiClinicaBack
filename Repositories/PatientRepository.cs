using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;

namespace ProjetoLP.API.Repositories;

public class PatientRepository(AppDbContext db) : IPatientRepository
{
    public async Task<(List<Patient> Items, int TotalCount)> GetPagedAsync(
        string? name,
        bool? isActive,
        AppointmentStatus? appointmentStatus,
        PaymentStatus? paymentStatus,
        int page,
        int pageSize)
    {
        var query = db.Patients
            .Include(p => p.Appointments)
            .Include(p => p.Payments)
            .Include(p => p.MedicalRecords)
            .AsQueryable();

        if (!string.IsNullOrEmpty(name))
            query = query.Where(p => p.Name != null && p.Name.Contains(name));

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (appointmentStatus.HasValue)
            query = query.Where(p => p.Appointments.Any(a => a.Status == appointmentStatus.Value));

        if (paymentStatus.HasValue)
            query = query.Where(p => p.Payments.Any(p => p.Status == paymentStatus.Value));

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Patient?> GetByIdAsync(int id)
        => await db.Patients
            .Include(p => p.Appointments)
            .Include(p => p.Payments)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Patient?> GetByIdWithDetailsAsync(int id)
        => await db.Patients
            .Include(p => p.Appointments).ThenInclude(a => a.User)
            .Include(p => p.MedicalRecords).ThenInclude(m => m.User)
            .Include(p => p.Payments).ThenInclude(p => p.Plan)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<bool> EmailExistsAsync(string? email, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var query = db.Patients.Where(p => p.Email == email);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<bool> CpfExistsAsync(string? cpf, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        var query = db.Patients.Where(p => p.CPF == cpf);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<bool> HasAssociatedRecordsAsync(int id)
        => await db.Appointments.AnyAsync(a => a.PatientId == id)
           || await db.Payments.AnyAsync(p => p.PatientId == id);

    public async Task<Patient> AddAsync(Patient patient)
    {
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    public async Task SaveChangesAsync()
        => await db.SaveChangesAsync();

    public async Task DeleteAsync(Patient patient)
    {
        db.Patients.Remove(patient);
        await db.SaveChangesAsync();
    }
}
