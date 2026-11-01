using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

// Métricas do dashboard por série (paciente + ficha de medição), sem acompanhamento.
public class EvolutionDashboardService(AppDbContext db, IUsuarioLogadoService usuario) : IEvolutionDashboardService
{
    private const int FollowUpWindowDays = 60;

    public async Task<EvolutionDashboardSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = monthStart.AddMonths(1);
        var windowStart = now.AddDays(-FollowUpWindowDays);

        // Só a janela de 60 dias vem para a memória; contagens históricas ficam no banco.
        var recentEvolutions = await db.PatientEvolutions
            .AsNoTracking()
            .Where(e => e.ClinicaId == usuario.ClinicaId
                && !e.IsDeleted
                && e.Status == EvolutionStatus.Completed
                && e.Date >= windowStart)
            .Include(e => e.Values)
                .ThenInclude(v => v.Field)
                .ThenInclude(f => f.Template)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var patientsInFollowUp = recentEvolutions
            .Where(e => e.Values.Any(v => !v.IsDeleted && v.ValueNumber.HasValue))
            .Select(e => e.PatientId)
            .Distinct()
            .Count();

        var completedEvolutionsThisMonth = await db.PatientEvolutions.CountAsync(e =>
            e.ClinicaId == usuario.ClinicaId && !e.IsDeleted && e.Status == EvolutionStatus.Completed
            && e.Date >= monthStart && e.Date < nextMonthStart);

        var seriesProgress = await GetSeriesProgressDataAsync(recentEvolutions);
        var patientClassifications = seriesProgress
            .GroupBy(s => s.PatientId)
            .Select(group => group.Average(s => s.DirectionScore))
            .ToList();

        var averageProgressValues = seriesProgress
            .Where(s => s.Progress.HasValue)
            .Select(s => s.Progress!.Value)
            .ToList();

        // Evoluções distintas que usaram cada ficha (todo o histórico), agregado no banco.
        var templateUsage = await db.PatientEvolutionValues
            .AsNoTracking()
            .Where(v => v.ClinicaId == usuario.ClinicaId && !v.IsDeleted
                && !v.Evolution.IsDeleted && v.Evolution.Status == EvolutionStatus.Completed)
            .Select(v => new { v.EvolutionId, v.Field.TemplateId, v.Field.Template.Name })
            .Distinct()
            .GroupBy(u => new { u.TemplateId, u.Name })
            .Select(g => new { g.Key.TemplateId, g.Key.Name, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Name)
            .Take(5)
            .ToListAsync();
        var mostUsedTemplates = templateUsage
            .Select(u => new MostUsedEvolutionTemplateDto { TemplateId = u.TemplateId, Name = u.Name, Count = u.Count })
            .ToList();

        return new EvolutionDashboardSummaryDto
        {
            PatientsInFollowUp = patientsInFollowUp,
            CompletedEvolutionsThisMonth = completedEvolutionsThisMonth,
            PatientsImproving = patientClassifications.Count(score => score > 0),
            PatientsStable = patientClassifications.Count(score => score == 0),
            PatientsWorsening = patientClassifications.Count(score => score < 0),
            AverageProgress = averageProgressValues.Count == 0
                ? null
                : Math.Round(averageProgressValues.Average(), 2),
            MostUsedTemplates = mostUsedTemplates
        };
    }

    private async Task<List<SeriesProgressData>> GetSeriesProgressDataAsync(List<PatientEvolution> evolutions)
    {
        var fields = await db.EvolutionTemplateFields
            .AsNoTracking()
            .Where(f => f.ClinicaId == usuario.ClinicaId
                && !f.IsDeleted
                && f.IsActive
                && f.ShowInChart)
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Label)
            .ToListAsync();

        var numericFields = fields.Where(f => IsNumericField(f.Type)).ToList();
        var templateByFieldId = numericFields.ToDictionary(f => f.Id, f => f.TemplateId);

        var result = new List<SeriesProgressData>();

        foreach (var templateFields in numericFields.GroupBy(f => f.TemplateId))
        {
            var templateId = templateFields.Key;

            var evolutionsByPatient = evolutions
                .Where(e => e.Values.Any(v =>
                    !v.IsDeleted
                    && v.ValueNumber.HasValue
                    && templateByFieldId.TryGetValue(v.FieldId, out var fieldTemplateId)
                    && fieldTemplateId == templateId))
                .GroupBy(e => e.PatientId);

            foreach (var patientEvolutions in evolutionsByPatient)
            {
                var ordered = patientEvolutions
                    .OrderBy(e => e.Date)
                    .ThenBy(e => e.CreatedAt)
                    .ToList();

                var charts = templateFields
                    .Select(field => EvolutionProgress.BuildProgressChart(field, ordered))
                    .Where(chart => chart is not null)
                    .Select(chart => chart!)
                    .ToList();

                if (charts.Count == 0)
                    continue;

                var progressValues = charts
                    .Where(chart => chart.Progress.HasValue)
                    .Select(chart => chart.Progress!.Value)
                    .ToList();

                result.Add(new SeriesProgressData(
                    patientEvolutions.Key,
                    charts.Average(DirectionScore),
                    progressValues.Count == 0 ? null : Math.Round(progressValues.Average(), 2)));
            }
        }

        return result;
    }

    private static int DirectionScore(TreatmentProgressChartDto chart)
        => EvolutionProgress.IsImproving(chart) ? 1
            : EvolutionProgress.IsWorsening(chart) ? -1
            : 0;

    private static bool IsNumericField(EvolutionFieldType type)
        => type is EvolutionFieldType.Number or EvolutionFieldType.Scale or EvolutionFieldType.Percentage or EvolutionFieldType.SelectScore;

    private sealed record SeriesProgressData(int PatientId, double DirectionScore, decimal? Progress);

}
