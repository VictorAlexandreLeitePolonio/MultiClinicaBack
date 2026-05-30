using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;

namespace ProjetoLP.API.Repositories;

public class PaymentRepository(AppDbContext db) : IPaymentRepository
{
    public async Task<(List<Payment> Items, int TotalCount)> GetPagedAsync(
        int? patientId,
        PaymentStatus? status,
        string? referenceMonth,
        string? patientName,
        int page,
        int pageSize)
    {
        var query = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .AsQueryable();

        if (!string.IsNullOrEmpty(patientName))
            query = query.Where(p => p.Patient.Name.Contains(patientName));

        if (patientId.HasValue)
            query = query.Where(p => p.PatientId == patientId.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (!string.IsNullOrEmpty(referenceMonth))
            query = query.Where(p => p.ReferenceMonth == referenceMonth);

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
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<bool> ExistsAsync(int patientId, string referenceMonth)
        => await db.Payments.AnyAsync(p =>
            p.PatientId == patientId && p.ReferenceMonth == referenceMonth);

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
        db.Payments.Remove(payment);
        await db.SaveChangesAsync();
    }
}
