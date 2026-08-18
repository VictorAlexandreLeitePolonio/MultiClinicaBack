using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.PatientPortal;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

/// <summary>
/// Endpoints privados do portal do paciente. Todas as consultas são escopadas
/// pelo <c>PatientAccountId</c> autenticado — nunca por clínica.
/// </summary>
public class PatientPortalService(AppDbContext db, IPatientAccountLoggedService logged) : IPatientPortalService
{
    private static string? DigitsOnly(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new string(value.Where(char.IsDigit).ToArray());

    public async Task<Result<PatientMeDto>> GetMeAsync()
    {
        var account = await db.PatientAccounts
            .FirstOrDefaultAsync(a => a.Id == logged.PatientAccountId && !a.IsDeleted);

        return account is null
            ? Result<PatientMeDto>.Fail(ErrorCodes.NotFound, "Conta não encontrada.")
            : Result<PatientMeDto>.Ok(MapMe(account));
    }

    public async Task<Result<PatientMeDto>> UpdateMeAsync(UpdatePatientMeDto dto)
    {
        var account = await db.PatientAccounts
            .FirstOrDefaultAsync(a => a.Id == logged.PatientAccountId && !a.IsDeleted);

        if (account is null)
            return Result<PatientMeDto>.Fail(ErrorCodes.NotFound, "Conta não encontrada.");

        // MVP: somente nome e telefone. CPF e e-mail permanecem read-only.
        if (!string.IsNullOrWhiteSpace(dto.Name))
            account.Name = dto.Name.Trim();
        account.Phone = DigitsOnly(dto.Phone) ?? account.Phone;

        await db.SaveChangesAsync();
        return Result<PatientMeDto>.Ok(MapMe(account));
    }

    public async Task<Result<IReadOnlyList<PatientAppointmentDto>>> GetUpcomingAppointmentsAsync()
    {
        var now = DateTime.UtcNow;
        var items = await BaseAppointments()
            .Where(a => a.AppointmentDate >= now && a.Status == AppointmentStatus.Scheduled)
            .OrderBy(a => a.AppointmentDate)
            .Select(MapAppointment)
            .ToListAsync();

        return Result<IReadOnlyList<PatientAppointmentDto>>.Ok(items);
    }

    public async Task<Result<IReadOnlyList<PatientAppointmentDto>>> GetHistoryAppointmentsAsync()
    {
        var now = DateTime.UtcNow;
        var items = await BaseAppointments()
            .Where(a => a.AppointmentDate < now || a.Status != AppointmentStatus.Scheduled)
            .OrderByDescending(a => a.AppointmentDate)
            .Select(MapAppointment)
            .ToListAsync();

        return Result<IReadOnlyList<PatientAppointmentDto>>.Ok(items);
    }

    public async Task<Result<IReadOnlyList<PatientClinicDto>>> GetClinicsAsync()
    {
        var clinics = await db.Patients
            .Where(p => p.PatientAccountId == logged.PatientAccountId && !p.IsDeleted)
            .Select(p => p.Clinica)
            .Distinct()
            .ToListAsync();

        // slug/coverUrl/categories/likeCount/likedByMe: BACK-5/BACK-6.
        // Contrato mantém os campos ("quando disponível") com defaults por ora.
        var result = clinics.Select(c => new PatientClinicDto
        {
            Id          = c.Id,
            DisplayName = string.IsNullOrWhiteSpace(c.DisplayName)
                ? (string.IsNullOrWhiteSpace(c.NomeFantasia) ? c.Nome : c.NomeFantasia)
                : c.DisplayName,
            LogoUrl    = c.LogoUrl,
            City       = c.Cidade,
            State      = c.Estado,
            Slug       = null,
            CoverUrl   = null,
            Categories = [],
            LikeCount  = 0,
            LikedByMe  = false,
        }).ToList();

        return Result<IReadOnlyList<PatientClinicDto>>.Ok(result);
    }

    // Base: consultas de qualquer clínica vinculadas ao PatientAccount autenticado.
    private IQueryable<Appointment> BaseAppointments()
        => db.Appointments
            .Include(a => a.Clinica)
            .Include(a => a.User)
            .Where(a => a.Patient.PatientAccountId == logged.PatientAccountId);

    private static readonly System.Linq.Expressions.Expression<Func<Appointment, PatientAppointmentDto>> MapAppointment =
        a => new PatientAppointmentDto
        {
            AppointmentId    = a.Id,
            ClinicId         = a.ClinicaId,
            ClinicName       = string.IsNullOrEmpty(a.Clinica.NomeFantasia) ? a.Clinica.Nome : a.Clinica.NomeFantasia,
            ClinicSlug       = null, // BACK-5
            ProfessionalName = a.User.Name,
            AppointmentDate  = a.AppointmentDate,
            Status           = a.Status,
        };

    private static PatientMeDto MapMe(PatientAccount account) => new()
    {
        Id     = account.Id,
        Name   = account.Name,
        Email  = account.Email,
        CPF    = account.CPF,
        Phone  = account.Phone,
        Status = account.Status,
    };
}
