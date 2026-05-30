using ProjetoLP.API.Common;
using ProjetoLP.API.Data;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Appointment;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;
using ProjetoLP.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ProjetoLP.API.Services;

public class AppointmentService(IAppointmentRepository repository, AppDbContext db) : IAppointmentService
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
            UserId          = a.UserId,
            UserName        = a.User.Name,
            PatientId       = a.PatientId,
            PatientName     = a.Patient.Name,
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

        return Result<AppointmentResponseDto>.Ok(new AppointmentResponseDto
        {
            Id              = appointment.Id,
            UserId          = appointment.UserId,
            UserName        = appointment.User.Name,
            AppointmentDate = DateTime.SpecifyKind(appointment.AppointmentDate, DateTimeKind.Utc),
            Status          = appointment.Status,
            PatientId       = appointment.PatientId,
            PatientName     = appointment.Patient.Name,
            CreatedAt       = DateTime.SpecifyKind(appointment.CreatedAt, DateTimeKind.Utc),
        });
    }

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<Result<AppointmentResponseDto>> CreateAsync(CreateAppointmentDto dto)
    {
        var user    = await db.Users.FindAsync(dto.UserId);
        var patient = await db.Patients.FindAsync(dto.PatientId);

        if (user is null)
            return Result<AppointmentResponseDto>.Fail(ErrorCodes.NotFound, "Usuário não encontrado.");
        if (patient is null)
            return Result<AppointmentResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        // Impede agendamento para paciente inativo
        if (!patient.IsActive)
            return Result<AppointmentResponseDto>.Fail(
                ErrorCodes.InactivePatient, "Não é possível agendar consulta para um paciente inativo.");

        // Impede agendamento no passado
        if (dto.AppointmentDate < DateTime.UtcNow)
            return Result<AppointmentResponseDto>.Fail(
                ErrorCodes.InvalidDate, "A data da consulta deve ser futura.");

        var appointment = new Appointment
        {
            UserId          = dto.UserId,
            PatientId       = dto.PatientId,
            AppointmentDate = dto.AppointmentDate,
        };

        await repository.AddAsync(appointment);

        return Result<AppointmentResponseDto>.Ok(new AppointmentResponseDto
        {
            Id              = appointment.Id,
            UserId          = appointment.UserId,
            UserName        = user.Name,
            PatientId       = appointment.PatientId,
            PatientName     = patient.Name,
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

        appointment.AppointmentDate = dto.AppointmentDate;
        appointment.Status          = dto.Status;

        await repository.SaveChangesAsync();

        // Recarrega para retornar dados atualizados
        var updated = await repository.GetByIdAsync(id);
        return Result<AppointmentResponseDto>.Ok(new AppointmentResponseDto
        {
            Id              = updated!.Id,
            UserId          = updated.UserId,
            UserName        = updated.User.Name,
            AppointmentDate = DateTime.SpecifyKind(updated.AppointmentDate, DateTimeKind.Utc),
            Status          = updated.Status,
            PatientId       = updated.PatientId,
            PatientName     = updated.Patient.Name,
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
