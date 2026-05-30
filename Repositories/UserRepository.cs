using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class UserRepository(AppDbContext db, IUsuarioLogadoService usuario) : IUserRepository
{
    public async Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        var query = db.Users.Where(u => u.ClinicaId == usuario.ClinicaId && !u.IsDeleted);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<User?> GetByIdAsync(int id)
        => await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.ClinicaId == usuario.ClinicaId && !u.IsDeleted);

    public async Task<User?> GetByEmailAsync(string email)
        => await db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

    public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
    {
        var query = db.Users.Where(u => u.Email == email && !u.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(u => u.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<int> CountAdminsAsync()
        => await db.Users.CountAsync(u => u.Role == UserRole.Administrador && u.ClinicaId == usuario.ClinicaId && !u.IsDeleted);

    public async Task<bool> HasAssociatedRecordsAsync(int id)
        => await db.Appointments.AnyAsync(a => a.UserId == id && a.ClinicaId == usuario.ClinicaId)
           || await db.MedicalRecords.AnyAsync(m => m.UserId == id && m.ClinicaId == usuario.ClinicaId)
           || await db.Payments.AnyAsync(p => p.UserId == id && p.ClinicaId == usuario.ClinicaId);

    public async Task<User> AddAsync(User user)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task SaveChangesAsync()
        => await db.SaveChangesAsync();

    public async Task DeleteAsync(User user)
    {
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.DeletedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
    }
}
