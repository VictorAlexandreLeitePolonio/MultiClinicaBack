using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;

namespace ProjetoLP.API.Repositories;

public class AppointmentRepository(AppDbContext db) : IAppointmentRepository
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
            query = query.Where(a => a.Patient.Name.Contains(patientName));

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
            .FirstOrDefaultAsync(a => a.Id == id);

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
        db.Appointments.Remove(appointment);
        await db.SaveChangesAsync();
    }
}
