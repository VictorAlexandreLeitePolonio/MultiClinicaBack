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

    // ── Evoluções do paciente ───────────────────────────────────────────────

    public async Task<Result<PagedResult<PatientEvolutionResponseDto>>> GetEvolutionsAsync(int patientId, int? appointmentId, int page, int pageSize)
    {
        if (!await PatientExistsAsync(patientId))
            return Result<PagedResult<PatientEvolutionResponseDto>>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.PatientEvolutions
            .AsNoTracking()
            .Include(e => e.Values)
            .ThenInclude(v => v.Field)
            .ThenInclude(f => f.Template)
            .Where(e => e.PatientId == patientId && e.ClinicaId == usuario.ClinicaId && !e.IsDeleted);

        if (appointmentId.HasValue)
            query = query.Where(e => e.AppointmentId == appointmentId.Value);

        var ordered = query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt);

        var total = await ordered.CountAsync();
        var data = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Result<PagedResult<PatientEvolutionResponseDto>>.Ok(new PagedResult<PatientEvolutionResponseDto>
        {
            Data = data.Select(ToEvolutionResponse),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<Result<PatientEvolutionResponseDto>> GetEvolutionAsync(int patientId, int evolutionId)
    {
        var evolution = await FindEvolutionAsync(patientId, evolutionId);
        return evolution is null
            ? Result<PatientEvolutionResponseDto>.Fail(ErrorCodes.NotFound, "Evolução não encontrada.")
            : Result<PatientEvolutionResponseDto>.Ok(ToEvolutionResponse(evolution));
    }

    public async Task<Result<PatientEvolutionResponseDto>> CreateEvolutionAsync(int patientId, CreatePatientEvolutionDto dto)
    {
        if (!await PatientExistsAsync(patientId))
            return Result<PatientEvolutionResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        var professionalId = await ResolveProfessionalIdAsync(dto.ProfessionalId);
        if (!professionalId.HasValue)
            return Result<PatientEvolutionResponseDto>.Fail(ErrorCodes.NotFound, "Profissional não encontrado.");

        if (dto.AppointmentId.HasValue && !await AppointmentBelongsToPatientAsync(patientId, dto.AppointmentId.Value))
            return Result<PatientEvolutionResponseDto>.Fail(ErrorCodes.InvalidValue, "A consulta não pertence a este paciente.");

        var validation = await ValidateValuesAsync(dto.Status, dto.Values);
        if (!validation.IsSuccess)
            return Result<PatientEvolutionResponseDto>.Fail(validation.ErrorCode!, validation.ErrorMessage!);

        var evolution = new PatientEvolution
        {
            ClinicaId = usuario.ClinicaId,
            PatientId = patientId,
            AppointmentId = dto.AppointmentId,
            ProfessionalId = professionalId.Value,
            ServiceId = dto.ServiceId,
            Date = ToUtc(dto.Date) ?? DateTime.UtcNow,
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

        // Recarrega com as navegações para montar as fichas medidas na resposta.
        var created = await db.PatientEvolutions
            .AsNoTracking()
            .Include(e => e.Values)
            .ThenInclude(v => v.Field)
            .ThenInclude(f => f.Template)
            .FirstAsync(e => e.Id == evolution.Id && e.ClinicaId == usuario.ClinicaId);

        return Result<PatientEvolutionResponseDto>.Ok(ToEvolutionResponse(created));
    }

    public async Task<Result<bool>> UpdateEvolutionAsync(int patientId, int evolutionId, UpdatePatientEvolutionDto dto)
    {
        var evolution = await FindEvolutionAsync(patientId, evolutionId);
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
            var validation = await ValidateValuesAsync(status, dto.Values);
            if (!validation.IsSuccess)
                return Result<bool>.Fail(validation.ErrorCode!, validation.ErrorMessage!);

            var now = DateTime.UtcNow;
            foreach (var oldValue in evolution.Values.Where(v => !v.IsDeleted))
            {
                oldValue.IsDeleted = true;
                oldValue.DeletedAt = now;
                oldValue.DeletedByUserId = usuario.UserId;
            }

            foreach (var newValue in BuildValues(dto.Values))
                evolution.Values.Add(newValue);
        }
        else if (status == EvolutionStatus.Completed)
        {
            var currentValues = evolution.Values
                .Where(v => !v.IsDeleted)
                .Select(v => new CreatePatientEvolutionValueDto
                {
                    FieldId = v.FieldId,
                    ValueNumber = v.ValueNumber,
                    ValueText = v.ValueText,
                    ValueBoolean = v.ValueBoolean,
                    ValueJson = v.ValueJson
                })
                .ToList();
            var validation = await ValidateValuesAsync(status, currentValues);
            if (!validation.IsSuccess)
                return Result<bool>.Fail(validation.ErrorCode!, validation.ErrorMessage!);
        }

        evolution.ServiceId = dto.ServiceId ?? evolution.ServiceId;
        evolution.Date = ToUtc(dto.Date) ?? evolution.Date;
        evolution.Description = dto.Description is null ? evolution.Description : NormalizeOptional(dto.Description);
        evolution.Conduct = dto.Conduct is null ? evolution.Conduct : NormalizeOptional(dto.Conduct);
        evolution.Observations = dto.Observations is null ? evolution.Observations : NormalizeOptional(dto.Observations);
        evolution.NextGuidance = dto.NextGuidance is null ? evolution.NextGuidance : NormalizeOptional(dto.NextGuidance);
        evolution.Status = status;
        evolution.UpdatedByUserId = usuario.UserId;

        await db.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> DeleteEvolutionAsync(int patientId, int evolutionId)
    {
        var evolution = await FindEvolutionAsync(patientId, evolutionId);
        if (evolution is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Evolução não encontrada.");

        evolution.IsDeleted = true;
        evolution.DeletedAt = DateTime.UtcNow;
        evolution.DeletedByUserId = usuario.UserId;
        await db.SaveChangesAsync();

        return Result<bool>.Ok(true);
    }

    private async Task<Result<bool>> ValidateValuesAsync(EvolutionStatus status, List<CreatePatientEvolutionValueDto> values)
    {
        var duplicateFieldId = values.GroupBy(v => v.FieldId).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicateFieldId.HasValue)
            return Result<bool>.Fail(ErrorCodes.InvalidValue, "Payload contém campo duplicado.");

        var fieldIds = values.Select(v => v.FieldId).Distinct().ToList();
        var fields = await db.EvolutionTemplateFields
            .Include(f => f.Template)
            .Where(f => fieldIds.Contains(f.Id)
                && f.ClinicaId == usuario.ClinicaId
                && !f.IsDeleted
                && f.IsActive
                && f.Template.ClinicaId == usuario.ClinicaId
                && !f.Template.IsDeleted
                && f.Template.IsActive)
            .ToListAsync();

        var fieldsById = fields.ToDictionary(f => f.Id);
        foreach (var value in values)
        {
            if (!fieldsById.TryGetValue(value.FieldId, out var field))
                return Result<bool>.Fail(ErrorCodes.InvalidValue, "Campo não pertence a uma ficha de medição ativa.");
            if (!ValueMatchesFieldType(field, value))
                return Result<bool>.Fail(ErrorCodes.InvalidValue, $"Valor inválido para o campo {field.Label}.");
            if (value.ValueNumber is { } number
                && ((field.MinValue.HasValue && number < field.MinValue.Value)
                    || (field.MaxValue.HasValue && number > field.MaxValue.Value)))
                return Result<bool>.Fail(ErrorCodes.InvalidValue, $"Valor fora da faixa permitida para o campo {field.Label}.");
        }

        if (status == EvolutionStatus.Completed)
        {
            // Só as fichas usadas na evolução exigem seus campos obrigatórios.
            var usedTemplateIds = fields.Select(f => f.TemplateId).Distinct().ToList();
            var requiredFields = await db.EvolutionTemplateFields
                .Include(f => f.Template)
                .Where(f => usedTemplateIds.Contains(f.TemplateId)
                    && f.ClinicaId == usuario.ClinicaId
                    && !f.IsDeleted
                    && f.IsActive
                    && f.Required
                    && f.Template.ClinicaId == usuario.ClinicaId
                    && !f.Template.IsDeleted
                    && f.Template.IsActive)
                .ToListAsync();

            var presentFieldIds = values.Select(v => v.FieldId).ToHashSet();
            var missingTemplate = requiredFields
                .OrderBy(f => f.Template.Name)
                .GroupBy(f => f.TemplateId)
                .FirstOrDefault(g => g.Any(f => !presentFieldIds.Contains(f.Id)));
            if (missingTemplate is not null)
                return Result<bool>.Fail(
                    ErrorCodes.EmptyField,
                    $"Preencha os campos obrigatórios da ficha {missingTemplate.First().Template.Name}.");
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

    private async Task<PatientEvolution?> FindEvolutionAsync(int patientId, int evolutionId)
        => await db.PatientEvolutions
            .Include(e => e.Values)
            .ThenInclude(v => v.Field)
            .ThenInclude(f => f.Template)
            .FirstOrDefaultAsync(e =>
                e.Id == evolutionId
                && e.PatientId == patientId
                && e.ClinicaId == usuario.ClinicaId
                && !e.IsDeleted);

    // Data sem fuso (Unspecified) é tratada como UTC; Local é convertida. O Postgres só aceita UTC.
    private static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Unspecified } v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        var v => v.Value.ToUniversalTime(),
    };

    private async Task<bool> AppointmentBelongsToPatientAsync(int patientId, int appointmentId)
        => await db.Appointments.AnyAsync(a =>
            a.Id == appointmentId
            && a.PatientId == patientId
            && a.ClinicaId == usuario.ClinicaId
            && !a.IsDeleted);

    private async Task<bool> PatientExistsAsync(int patientId)
        => await db.Patients.AnyAsync(p => p.Id == patientId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);

    private async Task<bool> ProfessionalExistsAsync(int professionalId)
        => await db.Users.AnyAsync(u => u.Id == professionalId && u.ClinicaId == usuario.ClinicaId && !u.IsDeleted);

    private async Task<bool> TemplateExistsAsync(int templateId, bool includeInactive)
        => await db.EvolutionTemplates.AnyAsync(t =>
            t.Id == templateId && t.ClinicaId == usuario.ClinicaId && !t.IsDeleted && (includeInactive || t.IsActive));

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

    private static PatientEvolutionResponseDto ToEvolutionResponse(PatientEvolution evolution)
    {
        var values = evolution.Values.Where(v => !v.IsDeleted).ToList();
        return new PatientEvolutionResponseDto
        {
            Id = evolution.Id,
            PatientId = evolution.PatientId,
            AppointmentId = evolution.AppointmentId,
            TemplateNames = values
                .Where(v => v.Field?.Template is not null)
                .OrderBy(v => v.Field.Order)
                .ThenBy(v => v.Field.TemplateId)
                .ThenBy(v => v.Field.Label)
                .Select(v => v.Field.Template.Name)
                .Distinct()
                .ToList(),
            ProfessionalId = evolution.ProfessionalId,
            ServiceId = evolution.ServiceId,
            Date = evolution.Date,
            Description = evolution.Description,
            Conduct = evolution.Conduct,
            Observations = evolution.Observations,
            NextGuidance = evolution.NextGuidance,
            Status = evolution.Status,
            Values = values.Select(v => new PatientEvolutionValueResponseDto
            {
                FieldId = v.FieldId,
                ValueNumber = v.ValueNumber,
                ValueText = v.ValueText,
                ValueBoolean = v.ValueBoolean,
                ValueJson = v.ValueJson
            }).ToList()
        };
    }

    private sealed record NormalizedField(
        EvolutionFieldUnit Unit,
        decimal? MinValue,
        decimal? MaxValue,
        decimal? TargetValue,
        bool ShowInChart);
}
