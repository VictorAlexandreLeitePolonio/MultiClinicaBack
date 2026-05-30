using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;

namespace ProjetoLP.API.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<List<User>> GetAllAsync()
        => await db.Users.ToListAsync();

    public async Task<User?> GetByIdAsync(int id)
        => await db.Users.FindAsync(id);

    public async Task<User?> GetByEmailAsync(string email)
        => await db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
    {
        var query = db.Users.Where(u => u.Email == email);
        if (excludeId.HasValue)
            query = query.Where(u => u.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<int> CountAdminsAsync()
        => await db.Users.CountAsync(u => u.Role == UserRole.Admin);

    public async Task<bool> HasAssociatedRecordsAsync(int id)
        => await db.Appointments.AnyAsync(a => a.UserId == id)
           || await db.MedicalRecords.AnyAsync(m => m.UserId == id)
           || await db.Payments.AnyAsync(p => p.UserId == id);

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
        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }
}
