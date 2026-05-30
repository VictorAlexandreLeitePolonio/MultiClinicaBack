using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class PatientRepository(AppDbContext db, IUsuarioLogadoService usuario) : IPatientRepository
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
            .Where(p => p.ClinicaId == usuario.ClinicaId && !p.IsDeleted)
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
            .FirstOrDefaultAsync(p => p.Id == id && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);

    public async Task<Patient?> GetByIdWithDetailsAsync(int id)
        => await db.Patients
            .Include(p => p.Appointments).ThenInclude(a => a.User)
            .Include(p => p.MedicalRecords).ThenInclude(m => m.User)
            .Include(p => p.Payments).ThenInclude(p => p.Plan)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);

    public async Task<bool> EmailExistsAsync(string? email, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var query = db.Patients.Where(p => p.Email == email && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<bool> CpfExistsAsync(string? cpf, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        var query = db.Patients.Where(p => p.CPF == cpf && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<bool> HasAssociatedRecordsAsync(int id)
        => await db.Appointments.AnyAsync(a => a.PatientId == id && a.ClinicaId == usuario.ClinicaId)
           || await db.Payments.AnyAsync(p => p.PatientId == id && p.ClinicaId == usuario.ClinicaId);

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
        patient.IsDeleted = true;
        patient.DeletedAt = DateTime.UtcNow;
        patient.DeletedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
    }
}
