using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services;

public class ClinicPlanService(AppDbContext db)
{
    private IQueryable<User> ActiveProfessionals(int clinicaId) => db.Users.Where(u =>
        u.ClinicaId == clinicaId && u.Role == UserRole.Profissional && u.IsActive && !u.IsDeleted);

    public Task<int> CountActiveProfessionalsAsync(int clinicaId) => ActiveProfessionals(clinicaId).CountAsync();

    public async Task<bool> CanAddProfessionalAsync(int clinicaId)
    {
        var clinic = await db.Clinicas.Where(c => c.Id == clinicaId)
            .Select(c => new { c.Plan, c.ExtraProfessionals }).SingleAsync();
        var limit = PlanMatrix.ProfessionalLimit(clinic.Plan, clinic.ExtraProfessionals);
        return limit is null || await CountActiveProfessionalsAsync(clinicaId) < limit;
    }

    // Checagem de vaga + gravação sob um lock por clínica: dois aceites simultâneos com uma vaga
    // leem a mesma contagem e cada um ativa outra linha, então só o lock serializa (chave 0 não
    // colide com o lock da agenda, que usa o id do profissional).
    public async Task<T> WithProfessionalSeatLockAsync<T>(int clinicaId, Func<Task<T>> body)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({clinicaId}, 0)");

        var result = await body();
        if (transaction is not null)
            await transaction.CommitAsync();
        return result;
    }

    // Não salva: o chamador agrupa com o resto da operação num único SaveChanges.
    public async Task ApplyPlanAsync(Clinica clinic, ClinicPlan plan, BillingCycle? cycle)
    {
        var previous = clinic.Plan;
        clinic.Plan = plan;
        clinic.BillingCycle = plan == ClinicPlan.Free ? null : cycle;
        if (previous != plan)
            db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
            {
                ClinicaId = clinic.Id,
                Type = CommercialHistoryEventType.PlanChanged,
                Description = $"Plano alterado de {previous} para {plan}.",
            });

        var limit = PlanMatrix.ProfessionalLimit(plan, clinic.ExtraProfessionals);
        if (limit is null) return;
        var excess = await ActiveProfessionals(clinic.Id)
            .OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
            .ToListAsync();
        var toDeactivate = excess.Take(Math.Max(0, excess.Count - limit.Value)).ToList();
        if (toDeactivate.Count == 0) return;
        foreach (var user in toDeactivate) user.IsActive = false;
        db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
        {
            ClinicaId = clinic.Id,
            Type = CommercialHistoryEventType.ProfessionalsDeactivatedByPlan,
            Description = $"{toDeactivate.Count} profissional(is) desativado(s) por limite do plano {plan}.",
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(toDeactivate.Select(u => u.Id)),
        });
    }
}
