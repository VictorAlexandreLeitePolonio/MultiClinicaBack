using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

// Prontuário do paciente: séries de medição por ficha e linha do tempo (consultas, evoluções e anamnese).
public class PatientRecordService(AppDbContext db, IUsuarioLogadoService usuario)
{
    private const int DefaultTimelineTake = 30;
    private const int MaxTimelineTake = 50;

    public async Task<Result<IReadOnlyList<PatientMeasurementSeriesDto>>> GetMeasurementsAsync(int patientId)
    {
        if (!await PatientExistsAsync(patientId))
            return Result<IReadOnlyList<PatientMeasurementSeriesDto>>.Fail(
                ErrorCodes.NotFound, "Paciente não encontrado.");

        var fields = await db.EvolutionTemplateFields
            .AsNoTracking()
            .Where(f => f.ClinicaId == usuario.ClinicaId && !f.IsDeleted && f.IsActive && f.ShowInChart)
            .Include(f => f.Template)
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Label)
            .ToListAsync();

        var evolutions = await db.PatientEvolutions
            .AsNoTracking()
            .Where(e => e.ClinicaId == usuario.ClinicaId
                && e.PatientId == patientId
                && !e.IsDeleted
                && e.Status == EvolutionStatus.Completed)
            .Include(e => e.Values)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var series = new List<PatientMeasurementSeriesDto>();

        foreach (var templateFields in fields.GroupBy(f => f.TemplateId))
        {
            var fieldIds = templateFields.Select(f => f.Id).ToHashSet();

            // Só entram evoluções que mediram algum campo da ficha; rascunhos já ficaram de fora na consulta.
            var templateEvolutions = evolutions
                .Where(e => e.Values.Any(v =>
                    !v.IsDeleted && fieldIds.Contains(v.FieldId) && v.ValueNumber.HasValue))
                .ToList();

            if (templateEvolutions.Count == 0)
                continue;

            var charts = templateFields
                .Select(field => EvolutionProgress.BuildProgressChart(field, templateEvolutions))
                .Where(chart => chart is not null)
                .Select(chart => chart!)
                .ToList();

            if (charts.Count == 0)
                continue;

            series.Add(new PatientMeasurementSeriesDto(
                templateFields.Key,
                templateFields.First().Template?.Name ?? string.Empty,
                templateEvolutions.Max(e => e.Date),
                templateEvolutions.Count,
                charts));
        }

        return Result<IReadOnlyList<PatientMeasurementSeriesDto>>.Ok(
            series.OrderByDescending(s => s.LastDate).ToList());
    }

    public async Task<Result<PatientTimelineDto>> GetTimelineAsync(int patientId, DateTime? before, int take)
    {
        if (!await PatientExistsAsync(patientId))
            return Result<PatientTimelineDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        // ?before=...Z chega como Kind=Local pelo model binding; o Postgres só aceita UTC.
        if (before is { } cursor)
            before = cursor.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(cursor, DateTimeKind.Utc)
                : cursor.ToUniversalTime();

        take = take <= 0 ? DefaultTimelineTake : Math.Min(take, MaxTimelineTake);
        // Recepção só enxerga os agendamentos, sem dados clínicos da consulta.
        var isReception = usuario.Role == UserRole.Recepcao;

        var ordered = await LoadTimelineAsync(patientId, isReception, before, null, take + 1);
        var page = ordered.Take(take).ToList();
        DateTime? nextBefore = null;
        if (ordered.Count > take)
        {
            var boundary = page[^1].Date;
            nextBefore = boundary;
            // Evoluções geradas da consulta herdam a data dela: o corte não pode separar um grupo
            // de mesma data, senão "Date < before" pula o resto.
            if (ordered[take].Date == boundary)
            {
                if (page.Any(e => e.Date != boundary))
                {
                    // O grupo vai inteiro para a próxima página.
                    page = page.Where(e => e.Date != boundary).ToList();
                    nextBefore = boundary.AddTicks(1);
                }
                else
                {
                    // A página toda é um grupo maior que "take": devolve o grupo completo.
                    page = await LoadTimelineAsync(patientId, isReception, before, boundary, null);
                }
            }
        }

        return Result<PatientTimelineDto>.Ok(new PatientTimelineDto(page, nextBefore));
    }

    // Eventos da linha do tempo; "at" busca um grupo de mesma data inteiro (sem limite).
    private async Task<List<PatientTimelineEventDto>> LoadTimelineAsync(
        int patientId, bool isReception, DateTime? before, DateTime? at, int? limit)
    {
        var appointmentQuery = db.Appointments
            .AsNoTracking()
            .Where(a => a.ClinicaId == usuario.ClinicaId && a.PatientId == patientId && !a.IsDeleted);

        if (before.HasValue)
            appointmentQuery = appointmentQuery.Where(a => a.AppointmentDate < before.Value);
        if (at.HasValue)
            appointmentQuery = appointmentQuery.Where(a => a.AppointmentDate == at.Value);

        var appointments = await appointmentQuery
            .Include(a => a.User)
            .OrderByDescending(a => a.AppointmentDate)
            .Take(limit ?? int.MaxValue)
            .ToListAsync();

        var appointmentIds = appointments.Select(a => a.Id).ToList();

        var reportStatuses = new Dictionary<int, ConsultationStatus>();
        var evolutionCounts = new Dictionary<int, int>();

        if (!isReception && appointmentIds.Count > 0)
        {
            reportStatuses = await db.ConsultationReports
                .AsNoTracking()
                .Where(r => r.ClinicaId == usuario.ClinicaId && appointmentIds.Contains(r.AppointmentId))
                .ToDictionaryAsync(r => r.AppointmentId, r => r.Status);

            evolutionCounts = await db.PatientEvolutions
                .AsNoTracking()
                .Where(e => e.ClinicaId == usuario.ClinicaId
                    && e.PatientId == patientId
                    && !e.IsDeleted
                    && e.AppointmentId.HasValue
                    && appointmentIds.Contains(e.AppointmentId.Value))
                .GroupBy(e => e.AppointmentId!.Value)
                .Select(group => new { AppointmentId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.AppointmentId, item => item.Count);
        }

        var evolutions = new List<PatientEvolution>();
        var anamneses = new List<MedicalRecord>();

        if (!isReception)
        {
            var evolutionQuery = db.PatientEvolutions
                .AsNoTracking()
                .Where(e => e.ClinicaId == usuario.ClinicaId && e.PatientId == patientId && !e.IsDeleted);

            if (before.HasValue)
                evolutionQuery = evolutionQuery.Where(e => e.Date < before.Value);
            if (at.HasValue)
                evolutionQuery = evolutionQuery.Where(e => e.Date == at.Value);

            evolutions = await evolutionQuery
                .Include(e => e.Professional)
                .Include(e => e.Values)
                    .ThenInclude(v => v.Field)
                    .ThenInclude(f => f.Template)
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.CreatedAt)
                .Take(limit ?? int.MaxValue)
                .ToListAsync();

            var anamnesisQuery = db.MedicalRecords
                .AsNoTracking()
                .Where(m => m.ClinicaId == usuario.ClinicaId && m.PatientId == patientId && !m.IsDeleted);

            if (before.HasValue)
                anamnesisQuery = anamnesisQuery.Where(m => m.CreatedAt < before.Value);
            if (at.HasValue)
                anamnesisQuery = anamnesisQuery.Where(m => m.CreatedAt == at.Value);

            anamneses = await anamnesisQuery
                .Include(m => m.User)
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit ?? int.MaxValue)
                .ToListAsync();
        }

        var events = new List<PatientTimelineEventDto>(appointments.Count + evolutions.Count + anamneses.Count);

        events.AddRange(appointments.Select(appointment => new PatientTimelineEventDto(
            "appointment",
            appointment.Id,
            appointment.AppointmentDate,
            "Consulta",
            appointment.User?.Name,
            appointment.Status.ToString(),
            isReception
                ? null
                : reportStatuses.TryGetValue(appointment.Id, out var status) ? status.ToString() : null,
            appointment.Id,
            isReception ? 0 : evolutionCounts.GetValueOrDefault(appointment.Id),
            [])));

        events.AddRange(evolutions.Select(evolution => new PatientTimelineEventDto(
            "evolution",
            evolution.Id,
            evolution.Date,
            "Evolução",
            evolution.Professional?.Name,
            evolution.Status.ToString(),
            null,
            evolution.AppointmentId,
            0,
            evolution.Values
                .Where(v => !v.IsDeleted && !string.IsNullOrWhiteSpace(v.Field?.Template?.Name))
                .Select(v => v.Field.Template.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToList())));

        events.AddRange(anamneses.Select(record => new PatientTimelineEventDto(
            "anamnesis",
            record.Id,
            record.CreatedAt,
            string.IsNullOrWhiteSpace(record.Titulo) ? "Anamnese" : record.Titulo.Trim(),
            record.User?.Name,
            null,
            null,
            null,
            0,
            [])));

        return events
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .ToList();
    }

    private async Task<bool> PatientExistsAsync(int patientId)
        => await db.Patients.AnyAsync(p =>
            p.Id == patientId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);
}
