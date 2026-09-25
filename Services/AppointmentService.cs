using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Appointment;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MultiClinica.API.Services;

public class AppointmentService(IAppointmentRepository repository, AppDbContext db, IUsuarioLogadoService usuario,
    AppointmentBookingGuard bookingGuard) : IAppointmentService
{
    // ── Listagem ─────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<AppointmentResponseDto>>> GetPagedAsync(
        AppointmentStatus? status, DateOnly? date, DateOnly? dateFrom,
        DateOnly? dateTo, string? patientName, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(
            status, date, dateFrom, dateTo, patientName, page, pageSize);

        var data = items.Select(a => new AppointmentResponseDto
        {
            Id              = a.Id,
            ProfessionalId  = a.UserId,
            UserName        = a.User.Name,
            PatientId       = a.PatientId,
            PatientName     = a.Patient.Name ?? string.Empty,
            AppointmentDate = DateTime.SpecifyKind(a.AppointmentDate, DateTimeKind.Utc),
            Status          = a.Status,
            CreatedAt       = DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc),
        });

        return Result<PagedResult<AppointmentResponseDto>>.Ok(new PagedResult<AppointmentResponseDto>
        {
            Data       = data,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        });
    }

    // ── Busca por Id ─────────────────────────────────────────────────────────

    public async Task<Result<AppointmentResponseDto>> GetByIdAsync(int id)
    {
        var appointment = await repository.GetByIdAsync(id);
        if (appointment is null)
            return Result<AppointmentResponseDto>.Fail(ErrorCodes.NotFound, "Consulta não encontrada.");

        var clinicTimeZone = await db.Clinicas.Where(c => c.Id == usuario.ClinicaId)
            .Select(c => c.TimeZoneId).FirstOrDefaultAsync();
        return Result<AppointmentResponseDto>.Ok(new AppointmentResponseDto
        {
            Id              = appointment.Id,
            ProfessionalId  = appointment.UserId,
            UserName        = appointment.User.Name,
            AppointmentDate = DateTime.SpecifyKind(appointment.AppointmentDate, DateTimeKind.Utc),
            Status          = appointment.Status,
            PatientId       = appointment.PatientId,
            PatientName     = appointment.Patient.Name ?? string.Empty,
            CreatedAt       = DateTime.SpecifyKind(appointment.CreatedAt, DateTimeKind.Utc),
            TimeZoneId      = clinicTimeZone,
        });
    }

    public async Task<Result<IReadOnlyList<AppointmentProfessionalDto>>> GetProfessionalsAsync()
    {
        var professionals = await db.Users.AsNoTracking()
            .Where(u => u.ClinicaId == usuario.ClinicaId && u.IsActive && !u.IsDeleted
                && (u.Role == UserRole.Profissional || u.Role == UserRole.Administrador))
            .OrderBy(u => u.Name)
            .Select(u => new AppointmentProfessionalDto(u.Id, u.Name))
            .ToListAsync();
        return Result<IReadOnlyList<AppointmentProfessionalDto>>.Ok(professionals);
    }

    public async Task<Result<ProfessionalDayScheduleDto>> GetDayScheduleAsync(int professionalId, DateOnly date)
    {
        var professional = await db.Users.AnyAsync(u => u.Id == professionalId && u.ClinicaId == usuario.ClinicaId
            && u.IsActive && !u.IsDeleted && (u.Role == UserRole.Profissional || u.Role == UserRole.Administrador));
        if (!professional)
            return Result<ProfessionalDayScheduleDto>.Fail(ErrorCodes.NotFound, "Profissional não encontrado.");
        var clinic = await db.Clinicas.AsNoTracking().FirstAsync(c => c.Id == usuario.ClinicaId);
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(clinic.TimeZoneId); }
        catch (TimeZoneNotFoundException)
        { return Result<ProfessionalDayScheduleDto>.Fail(ErrorCodes.InvalidDate, "Fuso da clínica inválido."); }
        catch (InvalidTimeZoneException)
        { return Result<ProfessionalDayScheduleDto>.Fail(ErrorCodes.InvalidDate, "Fuso da clínica inválido."); }

        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var dayStart = TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone);
        var dayEnd = TimeZoneInfo.ConvertTimeToUtc(localMidnight.AddDays(1), timeZone);
        var booked = await db.Appointments.AsNoTracking().Include(a => a.Patient)
            .Where(a => a.ClinicaId == usuario.ClinicaId && a.UserId == professionalId
                && a.Status == AppointmentStatus.Scheduled && !a.IsDeleted
                && a.AppointmentDate < dayEnd
                && a.AppointmentDate.AddMinutes(a.DurationMinutes) > dayStart)
            .OrderBy(a => a.AppointmentDate).ToListAsync();
        DateTimeOffset Local(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
            return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
        }
        var appointments = booked.Select(a => new DayAppointmentDto(a.Id, a.Patient.Name ?? string.Empty,
            Local(a.AppointmentDate), Local(a.AppointmentDate.AddMinutes(a.DurationMinutes)))).ToList();
        var duration = clinic.AppointmentSlotDurationMinutes > 0 ? clinic.AppointmentSlotDurationMinutes : 60;
        var slots = new List<DaySlotDto>();
        for (var local = localMidnight.AddHours(7); local < localMidnight.AddHours(20); local = local.AddMinutes(duration))
        {
            if (timeZone.IsInvalidTime(local) || timeZone.IsAmbiguousTime(local)) continue;
            var start = TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
            var end = start.AddMinutes(duration);
            slots.Add(new DaySlotDto(Local(start), Local(end),
                !booked.Any(a => start < a.AppointmentDate.AddMinutes(a.DurationMinutes)
                    && end > a.AppointmentDate)));
        }
        return Result<ProfessionalDayScheduleDto>.Ok(new ProfessionalDayScheduleDto(date, clinic.TimeZoneId,
            duration, appointments, slots));
    }

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<Result<AppointmentResponseDto>> CreateAsync(CreateAppointmentDto dto)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == dto.ProfessionalId && u.ClinicaId == usuario.ClinicaId
            && u.IsActive && !u.IsDeleted && (u.Role == UserRole.Profissional || u.Role == UserRole.Administrador));
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == dto.PatientId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);
        var clinic = await db.Clinicas.FirstOrDefaultAsync(c => c.Id == usuario.ClinicaId);

        if (user is null)
            return Result<AppointmentResponseDto>.Fail(ErrorCodes.NotFound, "Usuário não encontrado.");
        if (patient is null)
            return Result<AppointmentResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        // Impede agendamento para paciente inativo
        if (!patient.IsActive)
            return Result<AppointmentResponseDto>.Fail(
                ErrorCodes.InactivePatient, "Não é possível agendar consulta para um paciente inativo.");

        // Impede agendamento no passado
        var date = DateTime.SpecifyKind(dto.AppointmentDate, DateTimeKind.Utc);
        if (date < DateTime.UtcNow)
            return Result<AppointmentResponseDto>.Fail(
                ErrorCodes.InvalidDate, "A data da consulta deve ser futura.");

        var appointment = new Appointment
        {
            UserId          = dto.ProfessionalId,
            PatientId       = dto.PatientId,
            ClinicaId       = usuario.ClinicaId,
            AppointmentDate = date,
            DurationMinutes = clinic?.AppointmentSlotDurationMinutes > 0 ? clinic.AppointmentSlotDurationMinutes : 60,
            CreatedByUserId = usuario.UserId,
        };

        var booking = await bookingGuard.RunAsync(usuario.ClinicaId, dto.ProfessionalId, date,
            appointment.DurationMinutes, null, async () => await repository.AddAsync(appointment));
        if (!booking.IsSuccess)
            return Result<AppointmentResponseDto>.Fail(booking.ErrorCode!, booking.ErrorMessage!);

        return Result<AppointmentResponseDto>.Ok(new AppointmentResponseDto
        {
            Id              = appointment.Id,
            ProfessionalId  = appointment.UserId,
            UserName        = user.Name,
            PatientId       = appointment.PatientId,
            PatientName     = patient.Name ?? string.Empty,
            AppointmentDate = DateTime.SpecifyKind(appointment.AppointmentDate, DateTimeKind.Utc),
            Status          = appointment.Status,
            CreatedAt       = DateTime.SpecifyKind(appointment.CreatedAt, DateTimeKind.Utc),
        });
    }

    // ── Atualização ──────────────────────────────────────────────────────────

    public async Task<Result<AppointmentResponseDto>> UpdateAsync(int id, UpdateAppointmentDto dto)
    {
        var appointment = await repository.GetByIdAsync(id);
        if (appointment is null)
            return Result<AppointmentResponseDto>.Fail(ErrorCodes.NotFound, "Consulta não encontrada.");

        // Consulta concluída não pode voltar para Scheduled
        if (appointment.Status == AppointmentStatus.Completed && dto.Status == AppointmentStatus.Scheduled)
            return Result<AppointmentResponseDto>.Fail(
                ErrorCodes.CannotModify, "Não é possível reabrir uma consulta já concluída.");

        // Consulta cancelada não pode mudar de status
        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<AppointmentResponseDto>.Fail(
                ErrorCodes.CannotModify, "Não é possível alterar uma consulta cancelada.");

        if (dto.Status == AppointmentStatus.Scheduled && !await db.Users.AnyAsync(u => u.Id == appointment.UserId
                && u.ClinicaId == usuario.ClinicaId && u.IsActive && !u.IsDeleted
                && (u.Role == UserRole.Profissional || u.Role == UserRole.Administrador)))
            return Result<AppointmentResponseDto>.Fail(ErrorCodes.NotFound, "Profissional não encontrado.");

        var date = DateTime.SpecifyKind(dto.AppointmentDate, DateTimeKind.Utc);
        if (dto.Status == AppointmentStatus.Scheduled && date < DateTime.UtcNow)
            return Result<AppointmentResponseDto>.Fail(ErrorCodes.InvalidDate, "A data da consulta deve ser futura e válida.");

        async Task<bool> Save()
        {
            appointment.AppointmentDate = date;
            appointment.Status = dto.Status;
            appointment.UpdatedByUserId = usuario.UserId;
            await repository.SaveChangesAsync();
            return true;
        }

        if (dto.Status == AppointmentStatus.Scheduled)
        {
            var booking = await bookingGuard.RunAsync(usuario.ClinicaId, appointment.UserId, date,
                appointment.DurationMinutes, id, Save);
            if (!booking.IsSuccess)
                return Result<AppointmentResponseDto>.Fail(booking.ErrorCode!, booking.ErrorMessage!);
        }
        else
            await Save();

        // Recarrega para retornar dados atualizados
        var updated = await repository.GetByIdAsync(id);
        return Result<AppointmentResponseDto>.Ok(new AppointmentResponseDto
        {
            Id              = updated!.Id,
            ProfessionalId  = updated.UserId,
            UserName        = updated.User.Name,
            AppointmentDate = DateTime.SpecifyKind(updated.AppointmentDate, DateTimeKind.Utc),
            Status          = updated.Status,
            PatientId       = updated.PatientId,
            PatientName     = updated.Patient.Name ?? string.Empty,
            CreatedAt       = DateTime.SpecifyKind(updated.CreatedAt, DateTimeKind.Utc),
        });
    }

    // ── Deleção ──────────────────────────────────────────────────────────────

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var appointment = await repository.GetByIdAsync(id);
        if (appointment is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Consulta não encontrada.");

        // Impede deleção de consultas concluídas
        if (appointment.Status == AppointmentStatus.Completed)
            return Result<bool>.Fail(
                ErrorCodes.CannotDelete, "Não é possível excluir uma consulta já concluída.");

        await repository.DeleteAsync(appointment);
        return Result<bool>.Ok(true);
    }

}
