using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class AppointmentRepository(AppDbContext db, IUsuarioLogadoService usuario) : IAppointmentRepository
{
    public async Task<(List<Appointment> Items, int TotalCount)> GetPagedAsync(
        AppointmentStatus? status,
        DateOnly? date,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? patientName,
        int page,
        int pageSize)
    {
        var query = db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.User)
            .Where(a => a.ClinicaId == usuario.ClinicaId && !a.IsDeleted)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (date.HasValue)
            query = query.Where(a => DateOnly.FromDateTime(a.AppointmentDate) == date.Value);

        if (dateFrom.HasValue)
            query = query.Where(a => a.AppointmentDate >= dateFrom.Value.ToDateTime(TimeOnly.MinValue));

        if (dateTo.HasValue)
            query = query.Where(a => a.AppointmentDate <= dateTo.Value.ToDateTime(TimeOnly.MaxValue));

        if (!string.IsNullOrEmpty(patientName))
            query = query.Where(a => a.Patient.Name != null && a.Patient.Name.Contains(patientName));

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Appointment?> GetByIdAsync(int id)
        => await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClinicaId == usuario.ClinicaId && !a.IsDeleted);

    public async Task<Appointment> AddAsync(Appointment appointment)
    {
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }

    public async Task SaveChangesAsync()
        => await db.SaveChangesAsync();

    public async Task DeleteAsync(Appointment appointment)
    {
        appointment.IsDeleted = true;
        appointment.DeletedAt = DateTime.UtcNow;
        appointment.DeletedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
    }
}
