using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class EvolutionService(AppDbContext db, IUsuarioLogadoService usuario) : IEvolutionService
{
    public async Task<Result<List<EvolutionTemplateFieldResponseDto>>> GetFieldsAsync(int templateId)
    {
        if (!await TemplateExistsAsync(templateId, includeInactive: true))
            return Result<List<EvolutionTemplateFieldResponseDto>>.Fail(ErrorCodes.NotFound, "Modelo de evolução não encontrado.");

        var fields = await db.EvolutionTemplateFields
            .Where(f => f.TemplateId == templateId && f.ClinicaId == usuario.ClinicaId && !f.IsDeleted)
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Label)
            .Select(f => ToFieldResponse(f))
            .ToListAsync();

        return Result<List<EvolutionTemplateFieldResponseDto>>.Ok(fields);
    }

    public async Task<Result<EvolutionTemplateFieldResponseDto>> CreateFieldAsync(int templateId, CreateEvolutionTemplateFieldDto dto)
    {
        if (!await TemplateExistsAsync(templateId, includeInactive: false))
            return Result<EvolutionTemplateFieldResponseDto>.Fail(ErrorCodes.NotFound, "Modelo de evolução não encontrado.");

        var normalized = NormalizeField(dto);
        if (!normalized.IsSuccess)
            return Result<EvolutionTemplateFieldResponseDto>.Fail(normalized.ErrorCode!, normalized.ErrorMessage!);

        var key = await GenerateUniqueFieldKeyAsync(templateId, dto.Label);
        var field = new EvolutionTemplateField
        {
            ClinicaId = usuario.ClinicaId,
            TemplateId = templateId,
            Label = dto.Label.Trim(),
            Key = key,
            Type = dto.Type,
            Unit = normalized.Value!.Unit,
            MinValue = normalized.Value.MinValue,
            MaxValue = normalized.Value.MaxValue,
            TargetValue = normalized.Value.TargetValue,
            ExpectedDirection = dto.ExpectedDirection,
            Weight = dto.Weight <= 0 ? 1 : dto.Weight,
            Required = dto.Required,
            ShowInChart = normalized.Value.ShowInChart,
            Order = dto.Order,
            OptionsJson = NormalizeOptional(dto.OptionsJson),
            CreatedByUserId = usuario.UserId
        };

        db.EvolutionTemplateFields.Add(field);
        await db.SaveChangesAsync();

        return Result<EvolutionTemplateFieldResponseDto>.Ok(ToFieldResponse(field));
    }

    public async Task<Result<bool>> UpdateFieldAsync(int fieldId, UpdateEvolutionTemplateFieldDto dto)
    {
        var field = await db.EvolutionTemplateFields.FirstOrDefaultAsync(f =>
            f.Id == fieldId && f.ClinicaId == usuario.ClinicaId && !f.IsDeleted);

        if (field is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Campo de evolução não encontrado.");

        var isUsed = await db.PatientEvolutionValues.AnyAsync(v =>
            v.FieldId == field.Id && v.ClinicaId == usuario.ClinicaId && !v.IsDeleted);

        if (isUsed && (dto.Type.HasValue || dto.Unit.HasValue || dto.MinValue.HasValue ||
            dto.MaxValue.HasValue || dto.TargetValue.HasValue || dto.ExpectedDirection.HasValue ||
            dto.OptionsJson is not null))
            return Result<bool>.Fail(ErrorCodes.CannotModify, "Campo já usado em evolução permite apenas edição limitada.");

        if (dto.Label is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Label))
                return Result<bool>.Fail(ErrorCodes.EmptyField, "Label é obrigatório.");
            field.Label = dto.Label.Trim();
        }

        if (!isUsed)
        {
            field.Type = dto.Type ?? field.Type;
            field.Unit = dto.Unit ?? field.Unit;
            field.MinValue = dto.MinValue ?? field.MinValue;
            field.MaxValue = dto.MaxValue ?? field.MaxValue;
            field.TargetValue = dto.TargetValue ?? field.TargetValue;
            field.ExpectedDirection = dto.ExpectedDirection ?? field.ExpectedDirection;
            field.OptionsJson = dto.OptionsJson is null ? field.OptionsJson : NormalizeOptional(dto.OptionsJson);
        }

        field.Weight = dto.Weight ?? field.Weight;
        field.Required = dto.Required ?? field.Required;
        field.ShowInChart = dto.ShowInChart ?? field.ShowInChart;
        field.IsActive = dto.IsActive ?? field.IsActive;
        field.Order = dto.Order ?? field.Order;
        field.UpdatedByUserId = usuario.UserId;

        await db.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> DeactivateFieldAsync(int fieldId)
    {
        var field = await db.EvolutionTemplateFields.FirstOrDefaultAsync(f =>
            f.Id == fieldId && f.ClinicaId == usuario.ClinicaId && !f.IsDeleted);

        if (field is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Campo de evolução não encontrado.");

        field.IsActive = false;
        field.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();

        return Result<bool>.Ok(true);
    }

    public async Task<Result<List<PatientTreatmentResponseDto>>> GetTreatmentsAsync(int patientId)
    {
        if (!await PatientExistsAsync(patientId))
            return Result<List<PatientTreatmentResponseDto>>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        var treatments = await db.PatientTreatments
            .Where(t => t.PatientId == patientId && t.ClinicaId == usuario.ClinicaId && !t.IsDeleted)
            .OrderByDescending(t => t.StartedAt)
            .Select(t => ToTreatmentResponse(t))
            .ToListAsync();

        return Result<List<PatientTreatmentResponseDto>>.Ok(treatments);
    }

    public async Task<Result<PatientTreatmentResponseDto>> GetTreatmentAsync(int patientId, int treatmentId)
    {
        var treatment = await FindTreatmentAsync(patientId, treatmentId);
        return treatment is null
            ? Result<PatientTreatmentResponseDto>.Fail(ErrorCodes.NotFound, "Acompanhamento não encontrado.")
            : Result<PatientTreatmentResponseDto>.Ok(ToTreatmentResponse(treatment));
    }

    public async Task<Result<PatientTreatmentResponseDto>> CreateTreatmentAsync(int patientId, CreatePatientTreatmentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<PatientTreatmentResponseDto>.Fail(ErrorCodes.EmptyField, "Título é obrigatório.");
        if (!await PatientExistsAsync(patientId))
            return Result<PatientTreatmentResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");
        if (!await TemplateExistsAsync(dto.TemplateId, includeInactive: false))
            return Result<PatientTreatmentResponseDto>.Fail(ErrorCodes.NotFound, "Modelo de evolução não encontrado.");

        if (dto.ProfessionalId.HasValue && !await ProfessionalExistsAsync(dto.ProfessionalId.Value))
            return Result<PatientTreatmentResponseDto>.Fail(ErrorCodes.NotFound, "Profissional não encontrado.");

        var treatment = new PatientTreatment
        {
            ClinicaId = usuario.ClinicaId,
            PatientId = patientId,
            TemplateId = dto.TemplateId,
            ProfessionalId = dto.ProfessionalId ?? usuario.UserId,
            Title = dto.Title.Trim(),
            Description = NormalizeOptional(dto.Description),
            StartedAt = dto.StartedAt ?? DateTime.UtcNow,
            CreatedByUserId = usuario.UserId
        };

        db.PatientTreatments.Add(treatment);
        await db.SaveChangesAsync();

        return Result<PatientTreatmentResponseDto>.Ok(ToTreatmentResponse(treatment));
    }

    public async Task<Result<bool>> UpdateTreatmentAsync(int patientId, int treatmentId, UpdatePatientTreatmentDto dto)
    {
        var treatment = await FindTreatmentAsync(patientId, treatmentId);
        if (treatment is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Acompanhamento não encontrado.");

        if (dto.ProfessionalId.HasValue && !await ProfessionalExistsAsync(dto.ProfessionalId.Value))
            return Result<bool>.Fail(ErrorCodes.NotFound, "Profissional não encontrado.");
        if (dto.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return Result<bool>.Fail(ErrorCodes.EmptyField, "Título é obrigatório.");
            treatment.Title = dto.Title.Trim();
        }

        treatment.ProfessionalId = dto.ProfessionalId ?? treatment.ProfessionalId;
        treatment.Description = dto.Description is null ? treatment.Description : NormalizeOptional(dto.Description);
        treatment.StartedAt = dto.StartedAt ?? treatment.StartedAt;
        treatment.EndedAt = dto.EndedAt ?? treatment.EndedAt;
        treatment.Status = dto.Status ?? treatment.Status;
        treatment.UpdatedByUserId = usuario.UserId;

        await db.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    public async Task<Result<PagedResult<PatientEvolutionResponseDto>>> GetEvolutionsAsync(int patientId, int treatmentId, int page, int pageSize)
    {
        if (!await TreatmentExistsAsync(patientId, treatmentId))
            return Result<PagedResult<PatientEvolutionResponseDto>>.Fail(ErrorCodes.NotFound, "Acompanhamento não encontrado.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.PatientEvolutions
            .Include(e => e.Values)
            .Where(e => e.PatientId == patientId && e.TreatmentId == treatmentId && e.ClinicaId == usuario.ClinicaId && !e.IsDeleted)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt);

        var total = await query.CountAsync();
        var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Result<PagedResult<PatientEvolutionResponseDto>>.Ok(new PagedResult<PatientEvolutionResponseDto>
        {
            Data = data.Select(ToEvolutionResponse),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<Result<PatientEvolutionResponseDto>> GetEvolutionAsync(int patientId, int treatmentId, int evolutionId)
    {
        var evolution = await FindEvolutionAsync(patientId, treatmentId, evolutionId);
        return evolution is null
            ? Result<PatientEvolutionResponseDto>.Fail(ErrorCodes.NotFound, "Evolução não encontrada.")
            : Result<PatientEvolutionResponseDto>.Ok(ToEvolutionResponse(evolution));
    }

    public async Task<Result<TreatmentProgressResponseDto>> GetTreatmentProgressAsync(int patientId, int treatmentId)
    {
        var treatment = await FindTreatmentAsync(patientId, treatmentId);
        if (treatment is null)
            return Result<TreatmentProgressResponseDto>.Fail(ErrorCodes.NotFound, "Acompanhamento não encontrado.");

        var fields = await db.EvolutionTemplateFields
            .Where(f =>
                f.TemplateId == treatment.TemplateId
                && f.ClinicaId == usuario.ClinicaId
                && !f.IsDeleted
                && f.IsActive
                && f.ShowInChart)
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Label)
            .ToListAsync();

        var numericFields = fields
            .Where(f => IsNumericField(f.Type))
            .ToList();

        var evolutions = await db.PatientEvolutions
            .Include(e => e.Values)
            .Where(e =>
                e.PatientId == patientId
                && e.TreatmentId == treatmentId
                && e.ClinicaId == usuario.ClinicaId
                && !e.IsDeleted
                && e.Status == EvolutionStatus.Completed)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var charts = numericFields
            .Select(field => BuildProgressChart(field, evolutions))
            .Where(chart => chart is not null)
            .Select(chart => chart!)
            .ToList();

        var calculatedProgress = charts
            .Where(c => c.Progress.HasValue)
            .Select(c => c.Progress!.Value)
            .ToList();

        var response = new TreatmentProgressResponseDto
        {
            Treatment = new TreatmentProgressTreatmentDto
            {
                Id = treatment.Id,
                Title = treatment.Title
            },
            Summary = new TreatmentProgressSummaryDto
            {
                TotalEvolutions = evolutions.Count,
                OverallProgress = calculatedProgress.Count == 0 ? null : Math.Round(calculatedProgress.Average(), 2),
                ImprovingFields = charts.Count(c => IsImproving(c)),
                WorseningFields = charts.Count(c => IsWorsening(c)),
                StableFields = charts.Count(c => IsStable(c)),
                LastEvolutionDate = evolutions.LastOrDefault()?.Date
            },
            Charts = charts
        };

        return Result<TreatmentProgressResponseDto>.Ok(response);
    }

    public async Task<Result<PatientEvolutionResponseDto>> CreateEvolutionAsync(int patientId, int treatmentId, CreatePatientEvolutionDto dto)
    {
        var treatment = await FindTreatmentAsync(patientId, treatmentId);
        if (treatment is null)
            return Result<PatientEvolutionResponseDto>.Fail(ErrorCodes.NotFound, "Acompanhamento não encontrado.");

        var professionalId = await ResolveProfessionalIdAsync(dto.ProfessionalId);
        if (!professionalId.HasValue)
            return Result<PatientEvolutionResponseDto>.Fail(ErrorCodes.NotFound, "Profissional não encontrado.");

        var validation = await ValidateValuesAsync(treatment.TemplateId, dto.Status, dto.Values);
        if (!validation.IsSuccess)
            return Result<PatientEvolutionResponseDto>.Fail(validation.ErrorCode!, validation.ErrorMessage!);

        var evolution = new PatientEvolution
        {
            ClinicaId = usuario.ClinicaId,
            PatientId = patientId,
            TreatmentId = treatmentId,
            ProfessionalId = professionalId.Value,
            ServiceId = dto.ServiceId,
            Date = dto.Date ?? DateTime.UtcNow,
            Description = NormalizeOptional(dto.Description),
            Conduct = NormalizeOptional(dto.Conduct),
            Observations = NormalizeOptional(dto.Observations),
            NextGuidance = NormalizeOptional(dto.NextGuidance),
            Status = dto.Status,
            CreatedByUserId = usuario.UserId,
            Values = BuildValues(dto.Values)
        };

        db.PatientEvolutions.Add(evolution);
        await db.SaveChangesAsync();

        return Result<PatientEvolutionResponseDto>.Ok(ToEvolutionResponse(evolution));
    }

    public async Task<Result<bool>> UpdateEvolutionAsync(int patientId, int treatmentId, int evolutionId, UpdatePatientEvolutionDto dto)
    {
        var evolution = await FindEvolutionAsync(patientId, treatmentId, evolutionId);
        if (evolution is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Evolução não encontrada.");
        if (evolution.Status == EvolutionStatus.Canceled)
            return Result<bool>.Fail(ErrorCodes.CannotModify, "Evolução cancelada não pode ser editada.");

        var status = dto.Status ?? evolution.Status;
        if (dto.ProfessionalId.HasValue)
        {
            var professionalId = await ResolveProfessionalIdAsync(dto.ProfessionalId);
            if (!professionalId.HasValue)
                return Result<bool>.Fail(ErrorCodes.NotFound, "Profissional não encontrado.");
            evolution.ProfessionalId = professionalId.Value;
        }

        if (dto.Values is not null)
        {
            var validation = await ValidateValuesAsync(evolution.Treatment.TemplateId, status, dto.Values);
            if (!validation.IsSuccess)
                return Result<bool>.Fail(validation.ErrorCode!, validation.ErrorMessage!);

            db.PatientEvolutionValues.RemoveRange(evolution.Values);
            evolution.Values = BuildValues(dto.Values);
        }
        else if (status == EvolutionStatus.Completed)
        {
            var currentValues = evolution.Values.Select(v => new CreatePatientEvolutionValueDto
            {
                FieldId = v.FieldId,
                ValueNumber = v.ValueNumber,
                ValueText = v.ValueText,
                ValueBoolean = v.ValueBoolean,
                ValueJson = v.ValueJson
            }).ToList();
            var validation = await ValidateValuesAsync(evolution.Treatment.TemplateId, status, currentValues);
            if (!validation.IsSuccess)
                return Result<bool>.Fail(validation.ErrorCode!, validation.ErrorMessage!);
        }

        evolution.ServiceId = dto.ServiceId ?? evolution.ServiceId;
        evolution.Date = dto.Date ?? evolution.Date;
        evolution.Description = dto.Description is null ? evolution.Description : NormalizeOptional(dto.Description);
        evolution.Conduct = dto.Conduct is null ? evolution.Conduct : NormalizeOptional(dto.Conduct);
        evolution.Observations = dto.Observations is null ? evolution.Observations : NormalizeOptional(dto.Observations);
        evolution.NextGuidance = dto.NextGuidance is null ? evolution.NextGuidance : NormalizeOptional(dto.NextGuidance);
        evolution.Status = status;
        evolution.UpdatedByUserId = usuario.UserId;

        await db.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> DeleteEvolutionAsync(int patientId, int treatmentId, int evolutionId)
    {
        var evolution = await FindEvolutionAsync(patientId, treatmentId, evolutionId);
        if (evolution is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Evolução não encontrada.");

        evolution.IsDeleted = true;
        evolution.DeletedAt = DateTime.UtcNow;
        evolution.DeletedByUserId = usuario.UserId;
        await db.SaveChangesAsync();

        return Result<bool>.Ok(true);
    }

    private async Task<Result<bool>> ValidateValuesAsync(int templateId, EvolutionStatus status, List<CreatePatientEvolutionValueDto> values)
    {
        var duplicateFieldId = values.GroupBy(v => v.FieldId).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicateFieldId.HasValue)
            return Result<bool>.Fail(ErrorCodes.InvalidValue, "Payload contém campo duplicado.");

        var fields = await db.EvolutionTemplateFields
            .Where(f => f.TemplateId == templateId && f.ClinicaId == usuario.ClinicaId && !f.IsDeleted && f.IsActive)
            .ToListAsync();

        var fieldsById = fields.ToDictionary(f => f.Id);
        foreach (var value in values)
        {
            if (!fieldsById.TryGetValue(value.FieldId, out var field))
                return Result<bool>.Fail(ErrorCodes.InvalidValue, "Campo não pertence ao modelo do acompanhamento.");
            if (!ValueMatchesFieldType(field, value))
                return Result<bool>.Fail(ErrorCodes.InvalidValue, $"Valor inválido para o campo {field.Label}.");
        }

        if (status == EvolutionStatus.Completed)
        {
            var presentFieldIds = values.Select(v => v.FieldId).ToHashSet();
            var missingRequired = fields.FirstOrDefault(f => f.Required && !presentFieldIds.Contains(f.Id));
            if (missingRequired is not null)
                return Result<bool>.Fail(ErrorCodes.EmptyField, $"Campo obrigatório ausente: {missingRequired.Label}.");
        }

        return Result<bool>.Ok(true);
    }

    private static bool ValueMatchesFieldType(EvolutionTemplateField field, CreatePatientEvolutionValueDto value)
        => field.Type switch
        {
            EvolutionFieldType.Number or EvolutionFieldType.Scale or EvolutionFieldType.Percentage or EvolutionFieldType.SelectScore
                => value.ValueNumber.HasValue && value.ValueText is null && value.ValueBoolean is null,
            EvolutionFieldType.Boolean => value.ValueBoolean.HasValue && value.ValueNumber is null && value.ValueText is null,
            EvolutionFieldType.Text => value.ValueText is not null && value.ValueNumber is null && value.ValueBoolean is null,
            _ => false
        };

    private static TreatmentProgressChartDto? BuildProgressChart(EvolutionTemplateField field, List<PatientEvolution> evolutions)
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

    private static bool IsImproving(TreatmentProgressChartDto chart)
        => chart.Direction switch
        {
            EvolutionDirection.Increase => chart.CurrentValue > chart.InitialValue,
            EvolutionDirection.Decrease => chart.CurrentValue < chart.InitialValue,
            _ => false
        };

    private static bool IsWorsening(TreatmentProgressChartDto chart)
        => chart.Direction switch
        {
            EvolutionDirection.Increase => chart.CurrentValue < chart.InitialValue,
            EvolutionDirection.Decrease => chart.CurrentValue > chart.InitialValue,
            _ => false
        };

    private static bool IsStable(TreatmentProgressChartDto chart)
        => chart.Direction != EvolutionDirection.Neutral && chart.CurrentValue == chart.InitialValue;

    private List<PatientEvolutionValue> BuildValues(List<CreatePatientEvolutionValueDto> values)
        => values.Select(v => new PatientEvolutionValue
        {
            ClinicaId = usuario.ClinicaId,
            FieldId = v.FieldId,
            ValueNumber = v.ValueNumber,
            ValueText = NormalizeOptional(v.ValueText),
            ValueBoolean = v.ValueBoolean,
            ValueJson = NormalizeOptional(v.ValueJson),
            CreatedByUserId = usuario.UserId
        }).ToList();

    private async Task<int?> ResolveProfessionalIdAsync(int? requestedProfessionalId)
    {
        var professionalId = usuario.Role == UserRole.Profissional
            ? usuario.UserId
            : requestedProfessionalId ?? usuario.UserId;

        return await ProfessionalExistsAsync(professionalId) ? professionalId : null;
    }

    private async Task<bool> PatientExistsAsync(int patientId)
        => await db.Patients.AnyAsync(p => p.Id == patientId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);

    private async Task<bool> ProfessionalExistsAsync(int professionalId)
        => await db.Users.AnyAsync(u => u.Id == professionalId && u.ClinicaId == usuario.ClinicaId && !u.IsDeleted);

    private async Task<bool> TemplateExistsAsync(int templateId, bool includeInactive)
        => await db.EvolutionTemplates.AnyAsync(t =>
            t.Id == templateId && t.ClinicaId == usuario.ClinicaId && !t.IsDeleted && (includeInactive || t.IsActive));

    private async Task<PatientTreatment?> FindTreatmentAsync(int patientId, int treatmentId)
        => await db.PatientTreatments
            .Include(t => t.Template)
            .FirstOrDefaultAsync(t => t.Id == treatmentId && t.PatientId == patientId && t.ClinicaId == usuario.ClinicaId && !t.IsDeleted);

    private async Task<bool> TreatmentExistsAsync(int patientId, int treatmentId)
        => await db.PatientTreatments.AnyAsync(t =>
            t.Id == treatmentId && t.PatientId == patientId && t.ClinicaId == usuario.ClinicaId && !t.IsDeleted);

    private async Task<PatientEvolution?> FindEvolutionAsync(int patientId, int treatmentId, int evolutionId)
        => await db.PatientEvolutions
            .Include(e => e.Treatment)
            .Include(e => e.Values)
            .FirstOrDefaultAsync(e =>
                e.Id == evolutionId
                && e.PatientId == patientId
                && e.TreatmentId == treatmentId
                && e.ClinicaId == usuario.ClinicaId
                && !e.IsDeleted);

    private async Task<string> GenerateUniqueFieldKeyAsync(int templateId, string label)
    {
        var baseKey = Slugify(label);
        var key = baseKey;
        var suffix = 2;
        while (await db.EvolutionTemplateFields.AnyAsync(f =>
            f.TemplateId == templateId && f.Key == key && f.ClinicaId == usuario.ClinicaId && !f.IsDeleted))
        {
            key = $"{baseKey}_{suffix}";
            suffix++;
        }

        return key;
    }

    private static Result<NormalizedField> NormalizeField(CreateEvolutionTemplateFieldDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Label))
            return Result<NormalizedField>.Fail(ErrorCodes.EmptyField, "Label é obrigatório.");
        if (!Enum.IsDefined(dto.Type))
            return Result<NormalizedField>.Fail(ErrorCodes.InvalidValue, "Tipo de campo inválido.");
        if (dto.Unit.HasValue && !Enum.IsDefined(dto.Unit.Value))
            return Result<NormalizedField>.Fail(ErrorCodes.InvalidValue, "Unidade inválida.");
        if (dto.Type == EvolutionFieldType.SelectScore && !IsValidSelectScoreOptions(dto.OptionsJson))
            return Result<NormalizedField>.Fail(ErrorCodes.InvalidValue, "OptionsJson inválido para SelectScore.");

        var unit = dto.Unit ?? EvolutionFieldUnit.None;
        var min = dto.MinValue;
        var max = dto.MaxValue;
        var showInChart = IsNumericField(dto.Type) && dto.ShowInChart;

        if (dto.Type == EvolutionFieldType.Scale)
        {
            min ??= 0;
            max ??= 10;
        }

        if (dto.Type == EvolutionFieldType.Percentage)
        {
            unit = EvolutionFieldUnit.Percentage;
            min = 0;
            max = 100;
        }

        if (!IsNumericField(dto.Type))
        {
            unit = EvolutionFieldUnit.None;
            min = null;
            max = null;
            showInChart = false;
        }

        if (min.HasValue && max.HasValue && min.Value >= max.Value)
            return Result<NormalizedField>.Fail(ErrorCodes.InvalidValue, "MinValue deve ser menor que MaxValue.");
        if (dto.TargetValue.HasValue && min.HasValue && dto.TargetValue.Value < min.Value)
            return Result<NormalizedField>.Fail(ErrorCodes.InvalidValue, "TargetValue deve estar dentro do range.");
        if (dto.TargetValue.HasValue && max.HasValue && dto.TargetValue.Value > max.Value)
            return Result<NormalizedField>.Fail(ErrorCodes.InvalidValue, "TargetValue deve estar dentro do range.");

        return Result<NormalizedField>.Ok(new NormalizedField(unit, min, max, dto.TargetValue, showInChart));
    }

    private static bool IsNumericField(EvolutionFieldType type)
        => type is EvolutionFieldType.Number or EvolutionFieldType.Scale or EvolutionFieldType.Percentage or EvolutionFieldType.SelectScore;

    private static bool IsValidSelectScoreOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(optionsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var option in document.RootElement.EnumerateArray())
            {
                if (!option.TryGetProperty("label", out var label) || label.ValueKind != JsonValueKind.String)
                    return false;
                if (!option.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Number)
                    return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "campo" : slug;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EvolutionTemplateFieldResponseDto ToFieldResponse(EvolutionTemplateField field)
        => new()
        {
            Id = field.Id,
            TemplateId = field.TemplateId,
            Label = field.Label,
            Key = field.Key,
            Type = field.Type,
            Unit = field.Unit,
            MinValue = field.MinValue,
            MaxValue = field.MaxValue,
            TargetValue = field.TargetValue,
            ExpectedDirection = field.ExpectedDirection,
            Weight = field.Weight,
            Required = field.Required,
            ShowInChart = field.ShowInChart,
            IsActive = field.IsActive,
            Order = field.Order,
            OptionsJson = field.OptionsJson
        };

    private static PatientTreatmentResponseDto ToTreatmentResponse(PatientTreatment treatment)
        => new()
        {
            Id = treatment.Id,
            PatientId = treatment.PatientId,
            ProfessionalId = treatment.ProfessionalId,
            TemplateId = treatment.TemplateId,
            Title = treatment.Title,
            Description = treatment.Description,
            StartedAt = treatment.StartedAt,
            EndedAt = treatment.EndedAt,
            Status = treatment.Status,
            CreatedAt = treatment.CreatedAt
        };

    private static PatientEvolutionResponseDto ToEvolutionResponse(PatientEvolution evolution)
        => new()
        {
            Id = evolution.Id,
            PatientId = evolution.PatientId,
            TreatmentId = evolution.TreatmentId,
            ProfessionalId = evolution.ProfessionalId,
            ServiceId = evolution.ServiceId,
            Date = evolution.Date,
            Description = evolution.Description,
            Conduct = evolution.Conduct,
            Observations = evolution.Observations,
            NextGuidance = evolution.NextGuidance,
            Status = evolution.Status,
            Values = evolution.Values.Select(v => new PatientEvolutionValueResponseDto
            {
                FieldId = v.FieldId,
                ValueNumber = v.ValueNumber,
                ValueText = v.ValueText,
                ValueBoolean = v.ValueBoolean,
                ValueJson = v.ValueJson
            }).ToList()
        };

    private sealed record NormalizedField(
        EvolutionFieldUnit Unit,
        decimal? MinValue,
        decimal? MaxValue,
        decimal? TargetValue,
        bool ShowInChart);
}
