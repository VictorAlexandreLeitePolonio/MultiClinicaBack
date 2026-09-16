using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class SessionTypeRepository(AppDbContext db, IUsuarioLogadoService usuario)
{
    private IQueryable<SessionType> Scoped =>
        db.SessionTypes.Where(item => item.ClinicaId == usuario.ClinicaId);

    public Task<List<SessionType>> GetAllAsync(string? search)
    {
        var query = Scoped;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(item => item.Name.ToLower().Contains(normalizedSearch));
        }

        return query.OrderBy(item => item.Name).ToListAsync();
    }

    public Task<SessionType?> GetByIdAsync(int id) =>
        Scoped.FirstOrDefaultAsync(item => item.Id == id);

    public Task<bool> ExistsByNameAsync(string name) =>
        Scoped.AnyAsync(item => item.Name.ToLower() == name.ToLower());

    public async Task<SessionType> AddAsync(SessionType entity)
    {
        db.SessionTypes.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }
}
