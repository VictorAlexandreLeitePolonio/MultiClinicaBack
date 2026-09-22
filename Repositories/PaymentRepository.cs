using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class PaymentRepository(AppDbContext db, IUsuarioLogadoService usuario) : IPaymentRepository
{
    public async Task<(List<Payment> Items, int TotalCount)> GetPagedAsync(
        int? patientId,
        PaymentStatus? status,
        DateOnly? referenceMonth,
        string? patientName,
        int page,
        int pageSize)
    {
        var query = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .Where(p => p.ClinicaId == usuario.ClinicaId && !p.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(patientName))
            query = query.Where(p => p.Patient.Name != null && p.Patient.Name.Contains(patientName));

        if (patientId.HasValue)
            query = query.Where(p => p.PatientId == patientId.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (referenceMonth.HasValue)
        {
            var start = new DateOnly(referenceMonth.Value.Year, referenceMonth.Value.Month, 1);
            var end = start.AddMonths(1);
            query = query.Where(p => p.ReferenceMonth >= start && p.ReferenceMonth < end);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Payment?> GetByIdAsync(int id)
        => await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);

    public async Task<bool> ExistsAsync(int patientId, DateOnly referenceMonth, int? excludeId = null)
    {
        var start = new DateOnly(referenceMonth.Year, referenceMonth.Month, 1);
        var end = start.AddMonths(1);
        var query = db.Payments.Where(p =>
            p.PatientId == patientId
            && p.ReferenceMonth >= start
            && p.ReferenceMonth < end
            && p.ClinicaId == usuario.ClinicaId
            && !p.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<Payment> AddAsync(Payment payment)
    {
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    public async Task SaveChangesAsync()
        => await db.SaveChangesAsync();

    public async Task DeleteAsync(Payment payment)
    {
        payment.IsDeleted = true;
        payment.DeletedAt = DateTime.UtcNow;
        payment.DeletedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
    }
}
