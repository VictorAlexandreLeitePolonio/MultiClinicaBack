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

        // Dias no fuso da clínica viram intervalos UTC [início, fim) — o Postgres só aceita DateTime UTC.
        if (date.HasValue || dateFrom.HasValue || dateTo.HasValue)
        {
            var timeZone = await ClinicTimeZoneAsync();
            DateTime StartUtc(DateOnly day) => TimeZoneInfo.ConvertTimeToUtc(
                day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), timeZone);

            var from = date ?? dateFrom;
            var to = date ?? dateTo;
            if (from.HasValue)
            {
                var start = StartUtc(from.Value);
                query = query.Where(a => a.AppointmentDate >= start);
            }
            if (to.HasValue)
            {
                var end = StartUtc(to.Value.AddDays(1));
                query = query.Where(a => a.AppointmentDate < end);
            }
        }

        if (!string.IsNullOrEmpty(patientName))
            query = query.Where(a => a.Patient.Name != null && a.Patient.Name.Contains(patientName));

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    private async Task<TimeZoneInfo> ClinicTimeZoneAsync()
    {
        var id = await db.Clinicas.Where(c => c.Id == usuario.ClinicaId).Select(c => c.TimeZoneId).FirstOrDefaultAsync();
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? "America/Sao_Paulo" : id); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
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
