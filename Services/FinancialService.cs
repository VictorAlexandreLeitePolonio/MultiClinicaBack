using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public partial class FinancialService(AppDbContext db, IUsuarioLogadoService usuario) : IFinancialService
{
    [GeneratedRegex(@"^\d{2}-\d{4}$")]
    private static partial Regex ReferenceMonthRegex();

    public async Task<Result<FinancialBalanceDto>> GetBalanceAsync(string month)
    {
        if (!ReferenceMonthRegex().IsMatch(month))
            return Result<FinancialBalanceDto>.Fail(
                ErrorCodes.InvalidFormat, "O formato do mês deve ser 'MM-YYYY'. Exemplo: 03-2026");

        return Result<FinancialBalanceDto>.Ok(await BuildBalanceAsync(month));
    }

    public async Task<Result<List<FinancialBalanceDto>>> GetBalanceHistoryAsync(int months)
    {
        if (months < 1 || months > 24)
            return Result<List<FinancialBalanceDto>>.Fail(
                ErrorCodes.InvalidValue, "O período deve ser entre 1 e 24 meses.");

        var result = new List<FinancialBalanceDto>();
        var now = DateTime.UtcNow;

        for (int i = months - 1; i >= 0; i--)
        {
            var month = now.AddMonths(-i).ToString("MM-yyyy");
            result.Add(await BuildBalanceAsync(month));
        }

        return Result<List<FinancialBalanceDto>>.Ok(result);
    }

    private async Task<FinancialBalanceDto> BuildBalanceAsync(string month)
    {
        var parts = month.Split('-');
        var monthStart = new DateTime(int.Parse(parts[1]), int.Parse(parts[0]), 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = monthStart.AddMonths(1);
        var clinicaId = usuario.ClinicaId;

        var activePatients = await db.Patients.CountAsync(p =>
            p.ClinicaId == clinicaId && !p.IsDeleted && p.IsActive);

        var newPatients = await db.Patients.CountAsync(p =>
            p.ClinicaId == clinicaId && !p.IsDeleted
            && p.CreatedAt >= monthStart && p.CreatedAt < nextMonthStart);

        var appointmentsInMonth = db.Appointments.Where(a =>
            a.ClinicaId == clinicaId
            && a.AppointmentDate >= monthStart && a.AppointmentDate < nextMonthStart);

        var scheduledAppointments = await appointmentsInMonth.CountAsync(a => a.Status == AppointmentStatus.Scheduled);
        var completedAppointments = await appointmentsInMonth.CountAsync(a => a.Status == AppointmentStatus.Completed);
        var cancelledAppointments = await appointmentsInMonth.CountAsync(a => a.Status == AppointmentStatus.Cancelled);

        var completedEvolutions = await db.PatientEvolutions.CountAsync(e =>
            e.ClinicaId == clinicaId && !e.IsDeleted
            && e.Status == EvolutionStatus.Completed
            && e.Date >= monthStart && e.Date < nextMonthStart);

        var lowStockProducts = await db.Produtos.CountAsync(p =>
            p.ClinicaId == clinicaId && !p.IsDeleted
            && p.QuantidadeAtual < p.QuantidadeMinima);

        var stockMovements = await db.MovimentacoesEstoque.CountAsync(m =>
            m.ClinicaId == clinicaId && !m.IsCancelada
            && m.CreatedAt >= monthStart && m.CreatedAt < nextMonthStart);

        return new FinancialBalanceDto
        {
            ReferenceMonth = month,
            ActivePatients = activePatients,
            NewPatients = newPatients,
            ScheduledAppointments = scheduledAppointments,
            CompletedAppointments = completedAppointments,
            CancelledAppointments = cancelledAppointments,
            CompletedEvolutions = completedEvolutions,
            LowStockProducts = lowStockProducts,
            StockMovements = stockMovements,
        };
    }
}
