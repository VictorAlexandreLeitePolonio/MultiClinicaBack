using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services;

public sealed record UsageMetricDto(UsageMetric Metric, int Used, int PlanLimit, int ExtraGranted, int Total);
public sealed record UsageResponseDto(string Period, IReadOnlyList<UsageMetricDto> Metrics);

public class UsageService(AppDbContext db)
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static string CurrentPeriod(DateTime utcNow)
        => TimeZoneInfo.ConvertTimeFromUtc(utcNow, SaoPaulo).ToString("yyyy-MM");

    public async Task<bool> TryConsumeAsync(int clinicaId, UsageMetric metric, int amount)
    {
        var plan = await db.Clinicas.Where(c => c.Id == clinicaId).Select(c => c.Plan).SingleAsync();
        var limit = PlanMatrix.MonthlyQuota(plan, metric);
        var period = CurrentPeriod(DateTime.UtcNow);

        if (!db.Database.IsRelational())
        {
            // ponytail: caminho não atômico só para o provider InMemory dos testes; produção usa o UPDATE condicional abaixo.
            var row = await GetOrCreateAsync(clinicaId, period, metric);
            if (row.Used + amount > limit + row.ExtraGranted) return false;
            row.Used += amount;
            await db.SaveChangesAsync();
            return true;
        }

        var metricName = metric.ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ClinicUsages" ("ClinicaId", "Period", "Metric", "Used", "ExtraGranted")
            VALUES ({clinicaId}, {period}, {metricName}, 0, 0) ON CONFLICT DO NOTHING
            """);
        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ClinicUsages" SET "Used" = "Used" + {amount}
            WHERE "ClinicaId" = {clinicaId} AND "Period" = {period} AND "Metric" = {metricName}
              AND "Used" + {amount} <= {limit} + "ExtraGranted"
            """);
        return updated == 1;
    }

    public async Task GrantExtraAsync(int clinicaId, UsageMetric metric, int amount)
    {
        var period = CurrentPeriod(DateTime.UtcNow);
        if (!db.Database.IsRelational())
        {
            (await GetOrCreateAsync(clinicaId, period, metric)).ExtraGranted += amount;
            await db.SaveChangesAsync();
            return;
        }
        var metricName = metric.ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ClinicUsages" ("ClinicaId", "Period", "Metric", "Used", "ExtraGranted")
            VALUES ({clinicaId}, {period}, {metricName}, 0, {amount})
            ON CONFLICT ("ClinicaId", "Period", "Metric") DO UPDATE SET "ExtraGranted" = "ClinicUsages"."ExtraGranted" + {amount}
            """);
    }

    public async Task<UsageResponseDto> GetAsync(int clinicaId)
    {
        var plan = await db.Clinicas.Where(c => c.Id == clinicaId).Select(c => c.Plan).SingleAsync();
        var period = CurrentPeriod(DateTime.UtcNow);
        var rows = await db.ClinicUsages.AsNoTracking()
            .Where(u => u.ClinicaId == clinicaId && u.Period == period).ToListAsync();
        var metrics = Enum.GetValues<UsageMetric>().Select(metric =>
        {
            var row = rows.FirstOrDefault(r => r.Metric == metric);
            var limit = PlanMatrix.MonthlyQuota(plan, metric);
            var extra = row?.ExtraGranted ?? 0;
            return new UsageMetricDto(metric, row?.Used ?? 0, limit, extra, limit + extra);
        }).ToList();
        return new UsageResponseDto(period, metrics);
    }

    private async Task<ClinicUsage> GetOrCreateAsync(int clinicaId, string period, UsageMetric metric)
    {
        var row = await db.ClinicUsages.FindAsync(clinicaId, period, metric);
        if (row is not null) return row;
        row = new ClinicUsage { ClinicaId = clinicaId, Period = period, Metric = metric };
        db.ClinicUsages.Add(row);
        return row;
    }
}
