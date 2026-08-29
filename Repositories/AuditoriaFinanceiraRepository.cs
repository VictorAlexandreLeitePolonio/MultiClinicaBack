using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;

namespace MultiClinica.API.Repositories;

public class AuditoriaFinanceiraRepository(AppDbContext db) : IAuditoriaFinanceiraRepository
{
    public async Task AddAsync(AuditoriaFinanceira entity)
    {
        db.AuditoriasFinanceiras.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task<(List<AuditoriaFinanceiraDto> Items, int TotalCount)> GetPagedAsync(
        int clinicaId, string? modulo, string? entidade,
        DateTime? inicio, DateTime? fim, int page, int pageSize)
    {
        var query = db.AuditoriasFinanceiras.Where(a => a.ClinicaId == clinicaId);

        if (!string.IsNullOrWhiteSpace(modulo))
            query = query.Where(a => a.Modulo == modulo);
        if (!string.IsNullOrWhiteSpace(entidade))
            query = query.Where(a => a.Entidade == entidade);
        if (inicio.HasValue)
            query = query.Where(a => a.DataAcao >= inicio.Value);
        if (fim.HasValue)
            query = query.Where(a => a.DataAcao <= fim.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.DataAcao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditoriaFinanceiraDto
            {
                Id = a.Id,
                UsuarioId = a.UsuarioId,
                UsuarioNome = db.Users.Where(u => u.Id == a.UsuarioId).Select(u => u.Name).FirstOrDefault() ?? "",
                Modulo = a.Modulo,
                Acao = a.Acao,
                Entidade = a.Entidade,
                EntidadeId = a.EntidadeId,
                DadosAntes = a.DadosAntes,
                DadosDepois = a.DadosDepois,
                Motivo = a.Motivo,
                DataAcao = a.DataAcao
            })
            .ToListAsync();

        return (items, total);
    }
}
