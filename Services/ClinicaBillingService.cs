using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class ClinicaBillingService(AppDbContext db) : IClinicaBillingService
{
    public async Task GenerateMonthlyChargesAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var referenceMonth = today.ToString("yyyy-MM");
        var clinics = await db.Clinicas
            .Where(c => c.CobrancaAtiva && c.IsActive && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var clinic in clinics)
        {
            if (clinic.DataInicioCobranca.HasValue && clinic.DataInicioCobranca.Value > today)
                continue;

            var alreadyExists = await db.ClinicCharges.AnyAsync(c =>
                c.ClinicaId == clinic.Id && c.ReferenceMonth == referenceMonth, cancellationToken);

            if (alreadyExists)
                continue;

            var dueDay = Math.Clamp(clinic.DiaVencimento, 1, DateTime.DaysInMonth(today.Year, today.Month));
            db.ClinicCharges.Add(new ClinicCharge
            {
                ClinicaId = clinic.Id,
                ReferenceMonth = referenceMonth,
                Amount = clinic.ValorMensalidade,
                DueDate = new DateOnly(today.Year, today.Month, dueDay),
                Status = ClinicChargeStatus.Pending
            });

            db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
            {
                ClinicaId = clinic.Id,
                Type = CommercialHistoryEventType.ChargeCreated,
                Description = $"Cobrança {referenceMonth} criada automaticamente."
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task BlockOverdueClinicsAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var overdueClinicIds = await db.ClinicCharges
            .Where(c => c.Status == ClinicChargeStatus.Pending && c.DueDate < today)
            .Select(c => c.ClinicaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var clinics = await db.Clinicas
            .Where(c => overdueClinicIds.Contains(c.Id) && !c.IsBlockedByBilling)
            .ToListAsync(cancellationToken);

        foreach (var clinic in clinics)
        {
            clinic.IsBlockedByBilling = true;
            clinic.BillingBlockedAt = DateTime.UtcNow;
            clinic.BillingBlockReason = "Bloqueio automático por cobrança vencida.";
            db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
            {
                ClinicaId = clinic.Id,
                Type = CommercialHistoryEventType.AutomaticBillingBlock,
                Description = clinic.BillingBlockReason
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
