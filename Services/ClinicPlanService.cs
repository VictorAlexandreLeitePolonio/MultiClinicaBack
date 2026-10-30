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
