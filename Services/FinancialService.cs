using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class FinancialService(AppDbContext db, IUsuarioLogadoService usuario) : IFinancialService
{
    public async Task<Result<FinancialBalanceDto>> GetBalanceAsync(FinancialBalanceQueryDto query)
    {
        var now = DateTime.UtcNow;
        var defaultStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var periodStart = query.StartDate.HasValue
            ? DateTime.SpecifyKind(query.StartDate.Value.Date, DateTimeKind.Utc)
            : defaultStart;

        var periodEndExclusive = query.EndDate.HasValue
            ? DateTime.SpecifyKind(query.EndDate.Value.Date, DateTimeKind.Utc).AddDays(1)
            : defaultStart.AddMonths(1);

        if (periodStart >= periodEndExclusive)
            return Result<FinancialBalanceDto>.Fail(
                ErrorCodes.InvalidDate, "A data inicial não pode ser maior ou igual à data final.");

        var clinicaId = usuario.ClinicaId;

        var money = await BuildMoneySummaryAsync(clinicaId, periodStart, periodEndExclusive);
        var appointments = await BuildAppointmentsSummaryAsync(clinicaId, periodStart, periodEndExclusive);
        var patients = await BuildPatientsSummaryAsync(clinicaId, periodStart, periodEndExclusive);
        var evolutions = await BuildEvolutionsSummaryAsync(clinicaId, periodStart, periodEndExclusive);
        var recentMovements = await BuildRecentMovementsAsync(clinicaId, periodStart, periodEndExclusive);

        return Result<FinancialBalanceDto>.Ok(new FinancialBalanceDto
        {
            Period = new BalancePeriodDto { StartDate = periodStart, EndDate = periodEndExclusive.AddDays(-1) },
            Money = money,
            Appointments = appointments,
            Patients = patients,
            Evolutions = evolutions,
            RecentMovements = recentMovements
        });
    }

    private async Task<BalanceMoneySummaryDto> BuildMoneySummaryAsync(int clinicaId, DateTime start, DateTime endExclusive)
    {
        var startDate = DateOnly.FromDateTime(start);
        var endDateExclusive = DateOnly.FromDateTime(endExclusive);
        var paidPayments = await db.Payments
            .Where(p => p.ClinicaId == clinicaId && !p.IsDeleted && p.Status == PaymentStatus.Paid
                && (p.PaidAt ?? p.PaymentDate) >= startDate
                && (p.PaidAt ?? p.PaymentDate) < endDateExclusive)
            .Select(p => p.Amount)
            .ToListAsync();

        var manualExpenses = await db.ClinicExpenses
            .Where(e => e.ClinicaId == clinicaId && !e.IsDeleted
                && e.Date >= start && e.Date < endExclusive)
            .Select(e => e.Amount)
            .ToListAsync();

        var appointmentIncome = paidPayments.Sum();
        var manualExpenseCost = manualExpenses.Sum();

        return new BalanceMoneySummaryDto
        {
            AppointmentIncome = appointmentIncome,
            TotalIncome = appointmentIncome,
            ManualExpenseCost = manualExpenseCost,
            TotalOutcome = manualExpenseCost,
            EstimatedProfit = appointmentIncome - manualExpenseCost,
            PaidAppointmentCount = paidPayments.Count,
            ManualExpenseCount = manualExpenses.Count
        };
    }

    private async Task<BalanceAppointmentsSummaryDto> BuildAppointmentsSummaryAsync(int clinicaId, DateTime start, DateTime endExclusive)
    {
        var appointmentsInPeriod = db.Appointments.Where(a =>
            a.ClinicaId == clinicaId && a.AppointmentDate >= start && a.AppointmentDate < endExclusive);

        var scheduled = await appointmentsInPeriod.CountAsync(a => a.Status == AppointmentStatus.Scheduled);
        var completed = await appointmentsInPeriod.CountAsync(a => a.Status == AppointmentStatus.Completed);
        var cancelled = await appointmentsInPeriod.CountAsync(a => a.Status == AppointmentStatus.Cancelled);

        return new BalanceAppointmentsSummaryDto
        {
            Scheduled = scheduled,
            Completed = completed,
            Cancelled = cancelled,
            NoShow = 0, // não existe status de falta no AppointmentStatus atual
            Total = scheduled + completed + cancelled
        };
    }

    private async Task<BalancePatientsSummaryDto> BuildPatientsSummaryAsync(int clinicaId, DateTime start, DateTime endExclusive)
    {
        var active = await db.Patients.CountAsync(p => p.ClinicaId == clinicaId && !p.IsDeleted && p.IsActive);
        var total = await db.Patients.CountAsync(p => p.ClinicaId == clinicaId && !p.IsDeleted);
        var newInPeriod = await db.Patients.CountAsync(p =>
            p.ClinicaId == clinicaId && !p.IsDeleted && p.CreatedAt >= start && p.CreatedAt < endExclusive);

        return new BalancePatientsSummaryDto { Active = active, NewInPeriod = newInPeriod, Total = total };
    }

    private async Task<BalanceEvolutionSummaryDto> BuildEvolutionsSummaryAsync(int clinicaId, DateTime start, DateTime endExclusive)
    {
        var evolutionsInPeriod = await db.PatientEvolutions.CountAsync(e =>
            e.ClinicaId == clinicaId && !e.IsDeleted && e.Status == EvolutionStatus.Completed
            && e.Date >= start && e.Date < endExclusive);

        var treatmentsInProgress = await db.PatientTreatments.CountAsync(t =>
            t.ClinicaId == clinicaId && !t.IsDeleted && t.Status == TreatmentStatus.Active);

        var completedTreatments = await db.PatientTreatments.CountAsync(t =>
            t.ClinicaId == clinicaId && !t.IsDeleted && t.Status == TreatmentStatus.Completed);

        return new BalanceEvolutionSummaryDto
        {
            EvolutionsInPeriod = evolutionsInPeriod,
            TreatmentsInProgress = treatmentsInProgress,
            CompletedTreatments = completedTreatments
        };
    }

    private async Task<List<BalanceRecentMovementDto>> BuildRecentMovementsAsync(int clinicaId, DateTime start, DateTime endExclusive)
    {
        var startDate = DateOnly.FromDateTime(start);
        var endDateExclusive = DateOnly.FromDateTime(endExclusive);
        var payments = await db.Payments
            .Where(p => p.ClinicaId == clinicaId && !p.IsDeleted && p.Status == PaymentStatus.Paid
                && (p.PaidAt ?? p.PaymentDate) >= startDate
                && (p.PaidAt ?? p.PaymentDate) < endDateExclusive)
            .ToListAsync();

        var manualExpenses = await db.ClinicExpenses
            .Where(e => e.ClinicaId == clinicaId && !e.IsDeleted
                && e.Date >= start && e.Date < endExclusive)
            .ToListAsync();

        var fromPayments = payments.Select(p => new BalanceRecentMovementDto
        {
            Id = p.Id,
            Source = "Payment",
            Type = "AppointmentPayment",
            Description = $"Pagamento - {p.ReferenceMonth}",
            Amount = p.Amount,
            Quantity = null,
            Date = (p.PaidAt ?? p.PaymentDate)!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });

        var fromManualExpenses = manualExpenses.Select(e => new BalanceRecentMovementDto
        {
            Id = e.Id,
            Source = "ManualExpense",
            Type = "ClinicExpense",
            Description = e.Title,
            Amount = e.Amount,
            Quantity = null,
            Date = e.Date
        });

        return fromPayments.Concat(fromManualExpenses)
            .OrderByDescending(m => m.Date)
            .Take(10)
            .ToList();
    }
}
