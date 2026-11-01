using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services;

// Cálculo de progresso de um campo de ficha a partir das evoluções (gráficos, tendência, dashboard).
public static class EvolutionProgress
{
    public static TreatmentProgressChartDto? BuildProgressChart(EvolutionTemplateField field, List<PatientEvolution> evolutions)
    {
        var points = evolutions
            .Select(e => new
            {
                e.Date,
                Value = e.Values.FirstOrDefault(v =>
                    v.FieldId == field.Id
                    && !v.IsDeleted
                    && v.ValueNumber.HasValue)?.ValueNumber
            })
            .Where(point => point.Value.HasValue)
            .Select(point => new TreatmentProgressPointDto
            {
                Date = point.Date,
                Value = point.Value!.Value
            })
            .ToList();

        if (points.Count == 0)
            return null;

        var initialValue = points.First().Value;
        var currentValue = points.Last().Value;

        return new TreatmentProgressChartDto
        {
            FieldId = field.Id,
            Label = field.Label,
            Unit = field.Unit,
            Direction = field.ExpectedDirection,
            InitialValue = initialValue,
            CurrentValue = currentValue,
            TargetValue = field.TargetValue,
            Progress = field.TargetValue.HasValue
                ? CalculateProgress(initialValue, currentValue, field.TargetValue.Value, field.ExpectedDirection)
                : null,
            Data = points
        };
    }

    public static decimal? CalculateProgress(
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

    public static bool IsImproving(TreatmentProgressChartDto chart)
        => chart.Direction switch
        {
            EvolutionDirection.Increase => chart.CurrentValue > chart.InitialValue,
            EvolutionDirection.Decrease => chart.CurrentValue < chart.InitialValue,
            _ => false
        };

    public static bool IsWorsening(TreatmentProgressChartDto chart)
        => chart.Direction switch
        {
            EvolutionDirection.Increase => chart.CurrentValue < chart.InitialValue,
            EvolutionDirection.Decrease => chart.CurrentValue > chart.InitialValue,
            _ => false
        };

    public static bool IsStable(TreatmentProgressChartDto chart)
        => chart.Direction != EvolutionDirection.Neutral && chart.CurrentValue == chart.InitialValue;
}
