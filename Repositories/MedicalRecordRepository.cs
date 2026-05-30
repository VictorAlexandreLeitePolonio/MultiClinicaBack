using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;

namespace ProjetoLP.API.Repositories;

public class MedicalRecordRepository(AppDbContext db) : IMedicalRecordRepository
{
    public async Task<(List<MedicalRecord> Items, int TotalCount)> GetPagedAsync(
        int? patientId,
        string? patientName,
        int? userId,
        DateOnly? createdAt,
        int page,
        int pageSize)
    {
        var query = db.MedicalRecords
            .Include(m => m.User)
            .Include(m => m.Patient)
            .AsQueryable();

        if (patientId.HasValue)
            query = query.Where(m => m.PatientId == patientId.Value);

        if (!string.IsNullOrEmpty(patientName))
            query = query.Where(m => m.Patient.Name.Contains(patientName));

        if (userId.HasValue)
            query = query.Where(m => m.UserId == userId.Value);

        if (createdAt.HasValue)
            query = query.Where(m => DateOnly.FromDateTime(m.CreatedAt) == createdAt.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<MedicalRecord?> GetByIdAsync(int id)
        => await db.MedicalRecords
            .Include(m => m.User)
            .Include(m => m.Patient)
            .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<MedicalRecord> AddAsync(MedicalRecord medicalRecord)
    {
        db.MedicalRecords.Add(medicalRecord);
        await db.SaveChangesAsync();
        return medicalRecord;
    }

    public async Task SaveChangesAsync()
        => await db.SaveChangesAsync();

    public async Task DeleteAsync(MedicalRecord medicalRecord)
    {
        db.MedicalRecords.Remove(medicalRecord);
        await db.SaveChangesAsync();
    }
}
