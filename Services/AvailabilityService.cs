using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Availability;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public sealed class AvailabilityService(AppDbContext db, IUsuarioLogadoService usuario) : IAvailabilityService
{
    private const string SlotUnavailableMessage =
        "O horário selecionado não está mais disponível. Escolha outro horário.";
    private const string ProfessionalUnavailableMessage =
        "O profissional selecionado não está disponível nesse horário.";

    public async Task<Result<AvailabilitySettingsDto>> GetSettingsAsync()
    {
        var clinic = await CurrentClinicAsync();
        return clinic is null
            ? Result<AvailabilitySettingsDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.")
            : Result<AvailabilitySettingsDto>.Ok(MapSettings(clinic));
    }

    public async Task<Result<AvailabilitySettingsDto>> UpdateSettingsAsync(UpdateAvailabilitySettingsDto dto)
    {
        if (dto.SlotDurationMinutes is < 15 or > 240 || dto.SlotDurationMinutes % 5 != 0)
            return Result<AvailabilitySettingsDto>.Fail(
                ErrorCodes.InvalidValue,
                "A duração deve estar entre 15 e 240 minutos, em intervalos de 5 minutos.");

        if (!TryGetTimeZone(dto.TimeZoneId, out _))
            return Result<AvailabilitySettingsDto>.Fail(ErrorCodes.InvalidValue, "Fuso horário IANA inválido.");

        var clinic = await CurrentClinicAsync();
        if (clinic is null)
            return Result<AvailabilitySettingsDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        clinic.AppointmentSlotDurationMinutes = dto.SlotDurationMinutes;
        clinic.TimeZoneId = dto.TimeZoneId.Trim();
        clinic.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
        return Result<AvailabilitySettingsDto>.Ok(MapSettings(clinic));
    }

    public async Task<Result<IReadOnlyList<ProfessionalAvailabilityRangeDto>>> GetProfessionalAsync(int professionalId)
    {
        if (!await ProfessionalBelongsToCurrentClinicAsync(professionalId))
            return Result<IReadOnlyList<ProfessionalAvailabilityRangeDto>>.Fail(
                ErrorCodes.NotFound, "Profissional não encontrado.");

        var ranges = await db.ProfessionalAvailabilities
            .AsNoTracking()
            .Where(a => a.ClinicaId == usuario.ClinicaId && a.UserId == professionalId && !a.IsDeleted)
            .OrderBy(a => a.DayOfWeek)
            .ThenBy(a => a.StartTime)
            .Select(a => new ProfessionalAvailabilityRangeDto
            {
                DayOfWeek = a.DayOfWeek,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
            })
            .ToListAsync();

        return Result<IReadOnlyList<ProfessionalAvailabilityRangeDto>>.Ok(ranges);
    }

    public async Task<Result<IReadOnlyList<ProfessionalAvailabilityRangeDto>>> ReplaceProfessionalAsync(
        int professionalId, IReadOnlyList<ProfessionalAvailabilityRangeDto> ranges)
    {
        if (!await ProfessionalBelongsToCurrentClinicAsync(professionalId))
            return Result<IReadOnlyList<ProfessionalAvailabilityRangeDto>>.Fail(
                ErrorCodes.NotFound, "Profissional não encontrado.");

        var validationError = ValidateRanges(ranges);
        if (validationError is not null)
            return Result<IReadOnlyList<ProfessionalAvailabilityRangeDto>>.Fail(ErrorCodes.InvalidValue, validationError);

        var existing = await db.ProfessionalAvailabilities
            .Where(a => a.ClinicaId == usuario.ClinicaId && a.UserId == professionalId)
            .ToListAsync();
        db.ProfessionalAvailabilities.RemoveRange(existing);
        db.ProfessionalAvailabilities.AddRange(ranges.Select(range => new ProfessionalAvailability
        {
            ClinicaId = usuario.ClinicaId,
            UserId = professionalId,
            DayOfWeek = range.DayOfWeek,
            StartTime = range.StartTime,
            EndTime = range.EndTime,
            CreatedByUserId = usuario.UserId,
        }));
        await db.SaveChangesAsync();

        var response = ranges
            .OrderBy(r => r.DayOfWeek)
            .ThenBy(r => r.StartTime)
            .ToList();
        return Result<IReadOnlyList<ProfessionalAvailabilityRangeDto>>.Ok(response);
    }

    public async Task<Result<ClinicAvailabilityDto>> GetClinicAvailabilityAsync(int clinicId, DateOnly date)
    {
        var clinic = await db.Clinicas.AsNoTracking().FirstOrDefaultAsync(c =>
            c.Id == clinicId && c.IsActive && c.IsPublic && !c.IsDeleted && c.AcceptsAppointmentRequests);
        if (clinic is null)
            return Result<ClinicAvailabilityDto>.Fail(ErrorCodes.NotFound, "Clínica indisponível para agendamento.");

        if (!TryGetTimeZone(clinic.TimeZoneId, out var timeZone))
            return Result<ClinicAvailabilityDto>.Fail(ErrorCodes.InvalidValue, "Fuso horário da clínica é inválido.");

        var duration = clinic.AppointmentSlotDurationMinutes;
        var businessHours = await db.ClinicBusinessHours.AsNoTracking()
            .Where(h => h.ClinicaId == clinicId && h.DayOfWeek == date.DayOfWeek && h.IsActive && !h.IsDeleted)
            .OrderBy(h => h.StartTime)
            .ToListAsync();
        var professionalRanges = await db.ProfessionalAvailabilities.AsNoTracking()
            .Where(a => a.ClinicaId == clinicId && a.DayOfWeek == date.DayOfWeek && a.IsActive && !a.IsDeleted
                && a.User.IsActive && !a.User.IsDeleted)
            .ToListAsync();

        var dayStartLocal = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var dayEndLocal = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, timeZone);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayEndLocal, timeZone);
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.ClinicaId == clinicId && a.Status == AppointmentStatus.Scheduled && !a.IsDeleted
                && a.AppointmentDate < dayEndUtc
                && a.AppointmentDate.AddMinutes(a.DurationMinutes) > dayStartUtc)
            .Select(a => new BusyProfessional(a.UserId, a.AppointmentDate, a.DurationMinutes))
            .ToListAsync();
        var pendingRequests = await db.AppointmentRequests.AsNoTracking()
            .Where(r => r.ClinicaId == clinicId && r.Status == AppointmentRequestStatus.Pending && !r.IsDeleted
                && r.RequestedDate < dayEndUtc
                && r.RequestedDate.AddMinutes(r.DurationMinutes) > dayStartUtc)
            .Select(r => new BusyRequest(r.RequestedDate, r.DurationMinutes))
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var slots = new List<AvailabilitySlotDto>();
        foreach (var businessHour in businessHours)
        {
            for (var startTime = businessHour.StartTime;
                 startTime.AddMinutes(duration) <= businessHour.EndTime;
                 startTime = startTime.AddMinutes(duration))
            {
                var endTime = startTime.AddMinutes(duration);
                var localStart = date.ToDateTime(startTime, DateTimeKind.Unspecified);
                if (timeZone.IsInvalidTime(localStart))
                    continue;

                var offsetStart = new DateTimeOffset(localStart, timeZone.GetUtcOffset(localStart));
                if (offsetStart <= now)
                    continue;

                var localEnd = date.ToDateTime(endTime, DateTimeKind.Unspecified);
                var offsetEnd = new DateTimeOffset(localEnd, timeZone.GetUtcOffset(localEnd));
                var startUtc = offsetStart.UtcDateTime;
                var endUtc = offsetEnd.UtcDateTime;

                var coveringProfessionals = professionalRanges
                    .Where(a => a.StartTime <= startTime && a.EndTime >= endTime)
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count(userId => !appointments.Any(a => a.UserId == userId
                        && Overlaps(startUtc, endUtc, a.StartUtc, a.StartUtc.AddMinutes(a.DurationMinutes))));
                var reservations = pendingRequests.Count(r =>
                    Overlaps(startUtc, endUtc, r.StartUtc, r.StartUtc.AddMinutes(r.DurationMinutes)));
                var capacity = coveringProfessionals - reservations;
                if (capacity > 0)
                    slots.Add(new AvailabilitySlotDto { Start = offsetStart, End = offsetEnd, Capacity = capacity });
            }
        }

        return Result<ClinicAvailabilityDto>.Ok(new ClinicAvailabilityDto
        {
            Date = date,
            DurationMinutes = duration,
            TimeZoneId = clinic.TimeZoneId,
            Slots = slots,
        });
    }

    public async Task<Result<int>> ValidateRequestedSlotAsync(int clinicId, DateTimeOffset start)
    {
        var clinic = await db.Clinicas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId && !c.IsDeleted);
        if (clinic is null)
            return Result<int>.Fail(ErrorCodes.NotFound, "Clínica não encontrada ou inativa.");
        if (!clinic.IsActive || !clinic.IsPublic)
            return Result<int>.Fail(ErrorCodes.NotFound, "Clínica não encontrada ou inativa.");
        if (!clinic.AcceptsAppointmentRequests)
            return Result<int>.Fail(ErrorCodes.RequestsDisabled, "Esta clínica não aceita solicitações de consulta.");
        if (!TryGetTimeZone(clinic.TimeZoneId, out var timeZone))
            return Result<int>.Fail(ErrorCodes.InvalidValue, "Fuso horário da clínica é inválido.");

        var localStart = TimeZoneInfo.ConvertTime(start, timeZone);
        var availability = await GetClinicAvailabilityAsync(clinicId, DateOnly.FromDateTime(localStart.DateTime));
        var found = availability.Value?.Slots.Any(slot => slot.Start.ToUniversalTime() == start.ToUniversalTime()) == true;
        return found
            ? Result<int>.Ok(clinic.AppointmentSlotDurationMinutes)
            : Result<int>.Fail(ErrorCodes.SlotUnavailable, SlotUnavailableMessage);
    }

    public async Task<Result<bool>> ValidateProfessionalAsync(
        int clinicId, int professionalId, DateTime startUtc, int durationMinutes)
    {
        var clinic = await db.Clinicas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId && !c.IsDeleted);
        if (clinic is null || !TryGetTimeZone(clinic.TimeZoneId, out var timeZone))
            return Result<bool>.Fail(ErrorCodes.ProfessionalUnavailable, ProfessionalUnavailableMessage);

        var utcStart = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        var utcEnd = utcStart.AddMinutes(durationMinutes);
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(utcStart, timeZone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(utcEnd, timeZone);
        var covered = await db.ProfessionalAvailabilities.AnyAsync(a =>
            a.ClinicaId == clinicId && a.UserId == professionalId && a.DayOfWeek == localStart.DayOfWeek
            && a.StartTime <= TimeOnly.FromDateTime(localStart) && a.EndTime >= TimeOnly.FromDateTime(localEnd)
            && a.IsActive && !a.IsDeleted && a.User.IsActive && !a.User.IsDeleted);
        if (!covered)
            return Result<bool>.Fail(ErrorCodes.ProfessionalUnavailable, ProfessionalUnavailableMessage);

        var hasConflict = await db.Appointments.AnyAsync(a =>
            a.ClinicaId == clinicId && a.UserId == professionalId && a.Status == AppointmentStatus.Scheduled
            && !a.IsDeleted && utcStart < a.AppointmentDate.AddMinutes(a.DurationMinutes)
            && utcEnd > a.AppointmentDate);
        return hasConflict
            ? Result<bool>.Fail(ErrorCodes.ProfessionalUnavailable, ProfessionalUnavailableMessage)
            : Result<bool>.Ok(true);
    }

    private Task<Clinica?> CurrentClinicAsync() => db.Clinicas.FirstOrDefaultAsync(c =>
        c.Id == usuario.ClinicaId && !c.IsDeleted);

    private Task<bool> ProfessionalBelongsToCurrentClinicAsync(int professionalId) => db.Users.AnyAsync(u =>
        u.Id == professionalId && u.ClinicaId == usuario.ClinicaId && u.IsActive && !u.IsDeleted
        && (u.Role == UserRole.Profissional || u.Role == UserRole.Administrador));

    private static AvailabilitySettingsDto MapSettings(Clinica clinic) => new()
    {
        SlotDurationMinutes = clinic.AppointmentSlotDurationMinutes,
        TimeZoneId = clinic.TimeZoneId,
    };

    private static string? ValidateRanges(IReadOnlyList<ProfessionalAvailabilityRangeDto> ranges)
    {
        if (ranges.Any(r => r.StartTime >= r.EndTime))
            return "O horário inicial deve ser anterior ao horário final.";
        foreach (var day in ranges.GroupBy(r => r.DayOfWeek))
        {
            var ordered = day.OrderBy(r => r.StartTime).ToList();
            if (ordered.Zip(ordered.Skip(1), (left, right) => left.EndTime > right.StartTime).Any(overlap => overlap))
                return "As faixas de horário não podem se sobrepor.";
        }
        return null;
    }

    private static bool TryGetTimeZone(string? id, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id?.Trim() ?? string.Empty);
            return true;
        }
        catch (TimeZoneNotFoundException) { }
        catch (InvalidTimeZoneException) { }
        timeZone = TimeZoneInfo.Utc;
        return false;
    }

    private static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd)
        => start < otherEnd && end > otherStart;

    private sealed record BusyProfessional(int UserId, DateTime StartUtc, int DurationMinutes);
    private sealed record BusyRequest(DateTime StartUtc, int DurationMinutes);
}
