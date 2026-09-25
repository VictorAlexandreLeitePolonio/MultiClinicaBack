using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services;

public class AppointmentBookingGuard(AppDbContext db)
{
    public async Task<Result<T>> RunAsync<T>(int clinicId, int professionalId, DateTime startUtc,
        int durationMinutes, int? excludeAppointmentId, Func<Task<T>> save)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;

        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({clinicId}, {professionalId})");

        var endUtc = startUtc.AddMinutes(durationMinutes);
        var conflict = await db.Appointments.AnyAsync(a =>
            a.ClinicaId == clinicId && a.UserId == professionalId && !a.IsDeleted
            && a.Status == AppointmentStatus.Scheduled && a.Id != excludeAppointmentId
            && a.AppointmentDate < endUtc
            && a.AppointmentDate.AddMinutes(a.DurationMinutes) > startUtc);
        if (conflict)
            return Result<T>.Fail(ErrorCodes.AppointmentConflict, "Este horário já está ocupado.");

        var value = await save();
        if (transaction is not null)
            await transaction.CommitAsync();
        return Result<T>.Ok(value);
    }
}
