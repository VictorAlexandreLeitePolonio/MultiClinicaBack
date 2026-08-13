using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class FinancialService(AppDbContext db, IUsuarioLogadoService usuario) : IFinancialService
{
    private static readonly TipoMovimentacaoEstoque[] CostMovementTypes =
    [
        TipoMovimentacaoEstoque.Compra,
        TipoMovimentacaoEstoque.Saida,
        TipoMovimentacaoEstoque.Perda,
        TipoMovimentacaoEstoque.UsoInterno
    ];

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
        var stock = await BuildStockSummaryAsync(clinicaId, periodStart, periodEndExclusive);
        var evolutions = await BuildEvolutionsSummaryAsync(clinicaId, periodStart, periodEndExclusive);
        var recentMovements = await BuildRecentMovementsAsync(clinicaId, periodStart, periodEndExclusive);

        return Result<FinancialBalanceDto>.Ok(new FinancialBalanceDto
        {
            Period = new BalancePeriodDto { StartDate = periodStart, EndDate = periodEndExclusive.AddDays(-1) },
            Money = money,
            Appointments = appointments,
            Patients = patients,
            Stock = stock,
            Evolutions = evolutions,
            RecentMovements = recentMovements
        });
    }

    private async Task<BalanceMoneySummaryDto> BuildMoneySummaryAsync(int clinicaId, DateTime start, DateTime endExclusive)
    {
        var paidPayments = await db.Payments
            .Where(p => p.ClinicaId == clinicaId && !p.IsDeleted && p.Status == PaymentStatus.Paid
                && (p.PaidAt ?? p.PaymentDate) >= start && (p.PaidAt ?? p.PaymentDate) < endExclusive)
            .Select(p => p.Amount)
            .ToListAsync();

        var stockCostMovements = await db.MovimentacoesEstoque
            .Where(m => m.ClinicaId == clinicaId && !m.IsCancelada
                && CostMovementTypes.Contains(m.Tipo)
                && m.CreatedAt >= start && m.CreatedAt < endExclusive)
            .Select(m => new { m.Tipo, m.TotalValue })
            .ToListAsync();

        var productSales = await db.MovimentacoesEstoque
            .Where(m => m.ClinicaId == clinicaId && !m.IsCancelada
                && m.Tipo == TipoMovimentacaoEstoque.Venda
                && m.CreatedAt >= start && m.CreatedAt < endExclusive)
            .Select(m => m.TotalValue)
            .ToListAsync();

        var appointmentIncome = paidPayments.Sum();
        var productSalesIncome = productSales.Sum(v => v ?? 0);
        var totalIncome = appointmentIncome + productSalesIncome;

        var purchaseCost = stockCostMovements.Where(m => m.Tipo == TipoMovimentacaoEstoque.Compra).Sum(m => m.TotalValue ?? 0);
        var outputCost = stockCostMovements.Where(m => m.Tipo == TipoMovimentacaoEstoque.Saida).Sum(m => m.TotalValue ?? 0);
        var lossCost = stockCostMovements.Where(m => m.Tipo == TipoMovimentacaoEstoque.Perda).Sum(m => m.TotalValue ?? 0);
        var internalUseCost = stockCostMovements.Where(m => m.Tipo == TipoMovimentacaoEstoque.UsoInterno).Sum(m => m.TotalValue ?? 0);
        var totalOutcome = purchaseCost + outputCost + lossCost + internalUseCost;

        return new BalanceMoneySummaryDto
        {
            AppointmentIncome = appointmentIncome,
            ProductSalesIncome = productSalesIncome,
            TotalIncome = totalIncome,
            ProductPurchaseCost = purchaseCost,
            ProductOutputCost = outputCost,
            ProductLossCost = lossCost,
            ProductInternalUseCost = internalUseCost,
            TotalOutcome = totalOutcome,
            EstimatedProfit = totalIncome - totalOutcome,
            PaidAppointmentCount = paidPayments.Count,
            ProductSaleCount = productSales.Count,
            StockCostMovementCount = stockCostMovements.Count
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

    private async Task<BalanceStockSummaryDto> BuildStockSummaryAsync(int clinicaId, DateTime start, DateTime endExclusive)
    {
        var totalProducts = await db.Produtos.CountAsync(p => p.ClinicaId == clinicaId && !p.IsDeleted);

        var lowStockProducts = await db.Produtos
            .Where(p => p.ClinicaId == clinicaId && !p.IsDeleted && p.IsActive && p.QuantidadeAtual < p.QuantidadeMinima)
            .Select(p => new BalanceLowStockProductDto
            {
                ProductId = p.Id,
                Name = p.Nome,
                CurrentQuantity = p.QuantidadeAtual,
                MinimumQuantity = p.QuantidadeMinima
            })
            .ToListAsync();

        var movementsInPeriod = db.MovimentacoesEstoque.Where(m =>
            m.ClinicaId == clinicaId && !m.IsCancelada && m.CreatedAt >= start && m.CreatedAt < endExclusive);

        return new BalanceStockSummaryDto
        {
            TotalProducts = totalProducts,
            ProductsBelowMinimum = lowStockProducts.Count,
            StockEntriesInPeriod = await movementsInPeriod.CountAsync(m => m.Tipo == TipoMovimentacaoEstoque.Entrada),
            StockOutputsInPeriod = await movementsInPeriod.CountAsync(m => m.Tipo == TipoMovimentacaoEstoque.Saida),
            ProductSalesInPeriod = await movementsInPeriod.CountAsync(m => m.Tipo == TipoMovimentacaoEstoque.Venda),
            ProductPurchasesInPeriod = await movementsInPeriod.CountAsync(m => m.Tipo == TipoMovimentacaoEstoque.Compra),
            ProductLossesInPeriod = await movementsInPeriod.CountAsync(m => m.Tipo == TipoMovimentacaoEstoque.Perda),
            ProductInternalUseInPeriod = await movementsInPeriod.CountAsync(m => m.Tipo == TipoMovimentacaoEstoque.UsoInterno),
            LowStockProducts = lowStockProducts
        };
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
        var payments = await db.Payments
            .Where(p => p.ClinicaId == clinicaId && !p.IsDeleted && p.Status == PaymentStatus.Paid
                && (p.PaidAt ?? p.PaymentDate) >= start && (p.PaidAt ?? p.PaymentDate) < endExclusive)
            .ToListAsync();

        var stockMovements = await db.MovimentacoesEstoque
            .Include(m => m.Produto)
            .Where(m => m.ClinicaId == clinicaId && !m.IsCancelada
                && (m.Tipo == TipoMovimentacaoEstoque.Venda || CostMovementTypes.Contains(m.Tipo))
                && m.CreatedAt >= start && m.CreatedAt < endExclusive)
            .ToListAsync();

        var fromPayments = payments.Select(p => new BalanceRecentMovementDto
        {
            Id = p.Id,
            Source = "Payment",
            Type = "AppointmentPayment",
            Description = $"Pagamento - {p.ReferenceMonth}",
            Amount = p.Amount,
            Quantity = null,
            Date = (p.PaidAt ?? p.PaymentDate)!.Value
        });

        var fromStock = stockMovements.Select(m => new BalanceRecentMovementDto
        {
            Id = m.Id,
            Source = "Stock",
            Type = MapStockMovementType(m.Tipo),
            Description = m.Produto.Nome,
            Amount = m.TotalValue,
            Quantity = m.Quantidade,
            Date = m.CreatedAt
        });

        return fromPayments.Concat(fromStock)
            .OrderByDescending(m => m.Date)
            .Take(10)
            .ToList();
    }

    private static string MapStockMovementType(TipoMovimentacaoEstoque tipo) => tipo switch
    {
        TipoMovimentacaoEstoque.Venda => "ProductSale",
        TipoMovimentacaoEstoque.Compra => "ProductPurchase",
        TipoMovimentacaoEstoque.Saida => "ProductOutput",
        TipoMovimentacaoEstoque.Perda => "ProductLoss",
        TipoMovimentacaoEstoque.UsoInterno => "InternalUse",
        _ => tipo.ToString()
    };
}
