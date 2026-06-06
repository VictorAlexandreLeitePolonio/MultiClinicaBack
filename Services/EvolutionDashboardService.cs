using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class EvolutionDashboardService(AppDbContext db, IUsuarioLogadoService usuario) : IEvolutionDashboardService
{
    public async Task<EvolutionDashboardSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = monthStart.AddMonths(1);

        var activeTreatments = await db.PatientTreatments.CountAsync(t =>
            t.ClinicaId == usuario.ClinicaId
            && !t.IsDeleted
            && t.Status == TreatmentStatus.Active);

        var completedEvolutionsThisMonth = await db.PatientEvolutions.CountAsync(e =>
            e.ClinicaId == usuario.ClinicaId
            && !e.IsDeleted
            && e.Status == EvolutionStatus.Completed
            && e.Date >= monthStart
            && e.Date < nextMonthStart);

        var treatmentProgress = await GetTreatmentProgressDataAsync();
        var patientClassifications = treatmentProgress
            .GroupBy(t => t.PatientId)
            .Select(group => group.Average(t => t.DirectionScore))
            .ToList();

        var averageProgressValues = treatmentProgress
            .Where(t => t.Progress.HasValue)
            .Select(t => t.Progress!.Value)
            .ToList();

        var mostUsedTemplates = await db.PatientEvolutions
            .Where(e =>
                e.ClinicaId == usuario.ClinicaId
                && !e.IsDeleted
                && e.Status == EvolutionStatus.Completed)
            .GroupBy(e => new { e.Treatment.TemplateId, e.Treatment.Template.Name })
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.Name)
            .Take(5)
            .Select(group => new MostUsedEvolutionTemplateDto
            {
                TemplateId = group.Key.TemplateId,
                Name = group.Key.Name,
                Count = group.Count()
            })
            .ToListAsync();

        return new EvolutionDashboardSummaryDto
        {
            ActiveTreatments = activeTreatments,
            CompletedEvolutionsThisMonth = completedEvolutionsThisMonth,
            PatientsImproving = patientClassifications.Count(score => score > 0),
            PatientsStable = patientClassifications.Count(score => score == 0),
            PatientsWorsening = patientClassifications.Count(score => score < 0),
            AverageProgress = averageProgressValues.Count == 0 ? null : Math.Round(averageProgressValues.Average(), 2),
            MostUsedTemplates = mostUsedTemplates
        };
    }

    private async Task<List<TreatmentProgressData>> GetTreatmentProgressDataAsync()
    {
        var treatments = await db.PatientTreatments
            .Include(t => t.Template)
                .ThenInclude(t => t.Fields)
            .Include(t => t.Evolutions.Where(e =>
                !e.IsDeleted
                && e.Status == EvolutionStatus.Completed))
                .ThenInclude(e => e.Values)
            .Where(t => t.ClinicaId == usuario.ClinicaId && !t.IsDeleted)
            .ToListAsync();

        var result = new List<TreatmentProgressData>();

        foreach (var treatment in treatments)
        {
            var fieldProgress = treatment.Template.Fields
                .Where(f => f.ClinicaId == usuario.ClinicaId && !f.IsDeleted && f.IsActive && f.ShowInChart && IsNumericField(f.Type))
                .Select(field => BuildFieldProgress(field, treatment.Evolutions.OrderBy(e => e.Date).ThenBy(e => e.CreatedAt).ToList()))
                .Where(progress => progress is not null)
                .Select(progress => progress!)
                .ToList();

            if (fieldProgress.Count == 0)
                continue;

            var progressValues = fieldProgress
                .Where(p => p.Progress.HasValue)
                .Select(p => p.Progress!.Value)
                .ToList();

            result.Add(new TreatmentProgressData(
                treatment.PatientId,
                fieldProgress.Average(p => p.DirectionScore),
                progressValues.Count == 0 ? null : Math.Round(progressValues.Average(), 2)));
        }

        return result;
    }

    private static FieldProgressData? BuildFieldProgress(EvolutionTemplateField field, List<PatientEvolution> evolutions)
    {
        var points = evolutions
            .Select(e => e.Values.FirstOrDefault(v =>
                v.FieldId == field.Id
                && !v.IsDeleted
                && v.ValueNumber.HasValue)?.ValueNumber)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (points.Count == 0)
            return null;

        var initialValue = points.First();
        var currentValue = points.Last();

        return new FieldProgressData(
            GetDirectionScore(field.ExpectedDirection, initialValue, currentValue),
            field.TargetValue.HasValue
                ? CalculateProgress(initialValue, currentValue, field.TargetValue.Value, field.ExpectedDirection)
                : null);
    }

    private static int GetDirectionScore(EvolutionDirection direction, decimal initialValue, decimal currentValue)
        => direction switch
        {
            EvolutionDirection.Increase when currentValue > initialValue => 1,
            EvolutionDirection.Increase when currentValue < initialValue => -1,
            EvolutionDirection.Decrease when currentValue < initialValue => 1,
            EvolutionDirection.Decrease when currentValue > initialValue => -1,
            EvolutionDirection.Increase or EvolutionDirection.Decrease => 0,
            _ => 0
        };

    private static decimal? CalculateProgress(
        decimal initialValue,
        decimal currentValue,
        decimal targetValue,
        EvolutionDirection direction)
    {
        if (direction == EvolutionDirection.Neutral)
            return null;
        if (initialValue == targetValue)
            return 100;

        var denominator = direction == EvolutionDirection.Increase
            ? targetValue - initialValue
            : initialValue - targetValue;

        if (denominator == 0)
            return null;

        var progress = direction == EvolutionDirection.Increase
            ? ((currentValue - initialValue) / denominator) * 100
            : ((initialValue - currentValue) / denominator) * 100;

        return Math.Round(Math.Max(0, Math.Min(100, progress)), 2);
    }

    private static bool IsNumericField(EvolutionFieldType type)
        => type is EvolutionFieldType.Number or EvolutionFieldType.Scale or EvolutionFieldType.Percentage or EvolutionFieldType.SelectScore;

    private sealed record TreatmentProgressData(int PatientId, double DirectionScore, decimal? Progress);

    private sealed record FieldProgressData(int DirectionScore, decimal? Progress);
}
