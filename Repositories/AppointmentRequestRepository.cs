using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;

namespace MultiClinica.API.Repositories;

public class AppointmentRequestRepository(AppDbContext db) : IAppointmentRequestRepository
{
    public async Task<AppointmentRequest> AddAsync(AppointmentRequest request)
    {
        db.AppointmentRequests.Add(request);
        await db.SaveChangesAsync();
        return request;
    }

    public Task<AppointmentRequest?> GetForClinicAsync(int id, int clinicaId)
        => db.AppointmentRequests
            .Include(r => r.PatientAccount)
            .Include(r => r.Clinica)
            .FirstOrDefaultAsync(r => r.Id == id && r.ClinicaId == clinicaId && !r.IsDeleted);

    public Task<AppointmentRequest?> GetForPatientAsync(int id, int patientAccountId)
        => db.AppointmentRequests
            .Include(r => r.Clinica)
            .FirstOrDefaultAsync(r => r.Id == id && r.PatientAccountId == patientAccountId && !r.IsDeleted);

    public Task<List<AppointmentRequest>> ListForClinicAsync(int clinicaId)
        => db.AppointmentRequests
            .Include(r => r.PatientAccount)
            .Include(r => r.Clinica)
            .Where(r => r.ClinicaId == clinicaId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public Task<List<AppointmentRequest>> ListForPatientAsync(int patientAccountId)
        => db.AppointmentRequests
            .Include(r => r.Clinica)
            .Where(r => r.PatientAccountId == patientAccountId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public Task<Clinica?> GetClinicAsync(int clinicId)
        => db.Clinicas.FirstOrDefaultAsync(c => c.Id == clinicId && !c.IsDeleted);

    public Task<PatientAccount?> GetAccountAsync(int patientAccountId)
        => db.PatientAccounts.FirstOrDefaultAsync(a => a.Id == patientAccountId && !a.IsDeleted);

    public Task<Patient?> GetPatientLinkAsync(int patientAccountId, int clinicaId)
        => db.Patients.FirstOrDefaultAsync(p =>
            p.PatientAccountId == patientAccountId && p.ClinicaId == clinicaId && !p.IsDeleted);

    public Task<bool> ProfessionalBelongsToClinicAsync(int professionalId, int clinicaId)
        => db.Users.AnyAsync(u =>
            u.Id == professionalId && u.ClinicaId == clinicaId && !u.IsDeleted && u.IsActive);

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
