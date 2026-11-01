using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Consultation;
using MultiClinica.API.DTOs.Payment;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;
using MultiClinica.API.Services.MercadoPago;
using MultiClinica.API.Services.Transcription;

namespace MultiClinica.API.Services;

// Consulta gravada: início, pedaços, encerramento, relatório SOAP e as cascatas
// (cobrança da sessão, retorno e evolução).
public class ConsultationService(
    AppDbContext db,
    IUsuarioLogadoService usuario,
    ConsultationAudioStore audio,
    UsageService usage,
    IPaymentService payments,
    OnlinePaymentService onlinePayments,
    MercadoPagoAccounts mercadoPagoAccounts,
    TimeProvider time,
    ILogger<ConsultationService> logger)
{
    private const int MaxChunkBytes = 2 * 1024 * 1024;

    // Agendamento da clínica logada + relatório (se houver), com a regra de acesso do prontuário.
    private async Task<Result<Access>> LoadAsync(int appointmentId)
    {
        var appointment = await db.Appointments
            .FirstOrDefaultAsync(a => a.Id == appointmentId
                && a.ClinicaId == usuario.ClinicaId
                && !a.IsDeleted);

        if (appointment is null)
            return Result<Access>.Fail(ErrorCodes.NotFound, "Consulta não encontrada.");

        if (usuario.Role == UserRole.Profissional && appointment.UserId != usuario.UserId)
            return Result<Access>.Fail(ErrorCodes.Forbidden, "Você não tem acesso a esta consulta.");

        var report = await db.ConsultationReports
            .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId && r.ClinicaId == usuario.ClinicaId);

        return Result<Access>.Ok(new Access(appointment, report));
    }

    // ── Início e gravação ────────────────────────────────────────────────────

    public async Task<Result<StartConsultationResponse>> StartAsync(
        int appointmentId, StartConsultationDto dto, CancellationToken ct = default)
    {
        var access = await LoadAsync(appointmentId);
        if (!access.IsSuccess)
            return Result<StartConsultationResponse>.Fail(access.ErrorCode!, access.ErrorMessage!);

        var appointment = access.Value!.Appointment;
        var report = access.Value.Report;

        if (!dto.ConsentConfirmed)
            return Result<StartConsultationResponse>.Fail(
                ErrorCodes.ConsentRequired, "O paciente precisa autorizar a gravação da consulta.");

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<StartConsultationResponse>.Fail(
                ErrorCodes.AppointmentCancelled, "Não é possível iniciar uma consulta cancelada.");

        if (report is not null)
            return Result<StartConsultationResponse>.Fail(
                ErrorCodes.ConsultationExists, "Esta consulta já possui um relatório.");

        var transcription = (await usage.GetAsync(usuario.ClinicaId)).Metrics
            .First(m => m.Metric == UsageMetric.TranscriptionMinutes);
        if (transcription.Used >= transcription.Total)
            return Result<StartConsultationResponse>.Fail(
                ErrorCodes.TranscriptionQuotaExhausted, "A cota de transcrição do mês acabou.");

        var now = time.GetUtcNow().UtcDateTime;
        report = new ConsultationReport
        {
            ClinicaId = usuario.ClinicaId,
            AppointmentId = appointment.Id,
            PatientId = appointment.PatientId,
            ProfessionalId = appointment.UserId,
            RecordedByUserId = usuario.UserId,
            ConsentConfirmedAt = now,
            Status = ConsultationStatus.Recording,
            StartedAt = now,
            CreatedByUserId = usuario.UserId,
        };
        db.ConsultationReports.Add(report);
        appointment.Status = AppointmentStatus.Completed;
        appointment.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync(ct);

        return Result<StartConsultationResponse>.Ok(new StartConsultationResponse(report.Id));
    }

    public async Task<Result<bool>> SaveChunkAsync(
        int appointmentId, int n, Stream body, long? contentLength, CancellationToken ct = default)
    {
        if (n < 0 || n >= 2000)
            return Result<bool>.Fail(ErrorCodes.InvalidValue, "Pedaço de áudio inválido.");

        var access = await LoadAsync(appointmentId);
        if (!access.IsSuccess)
            return Result<bool>.Fail(access.ErrorCode!, access.ErrorMessage!);

        var report = access.Value!.Report;
        if (report is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Relatório não encontrado.");

        if (report.Status is not (ConsultationStatus.Recording or ConsultationStatus.Interrupted))
            return Result<bool>.Fail(
                ErrorCodes.InvalidStatus, "A consulta não está gravando.");

        // Lê no máximo 2 MB + 1 byte para saber se o pedaço passou do limite.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0;
        while (total <= MaxChunkBytes)
        {
            var read = await body.ReadAsync(
                chunk.AsMemory(0, Math.Min(chunk.Length, MaxChunkBytes + 1 - total)), ct);
            if (read == 0) break;
            buffer.Write(chunk, 0, read);
            total += read;
        }

        if (total > MaxChunkBytes)
            return Result<bool>.Fail(ErrorCodes.FileTooLarge, "O pedaço de áudio excede o limite de 2 MB.");

        buffer.Position = 0;
        await audio.SaveChunkAsync(report.Id, n, buffer, ct);

        var now = time.GetUtcNow().UtcDateTime;
        report.ChunkCount = Math.Max(report.ChunkCount, n + 1);
        report.LastChunkAt = now;
        if (report.Status == ConsultationStatus.Interrupted)
            report.Status = ConsultationStatus.Recording;
        report.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync(ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<FinishConsultationResponse>> FinishAsync(
        int appointmentId, FinishConsultationDto dto, CancellationToken ct = default)
    {
        var access = await LoadAsync(appointmentId);
        if (!access.IsSuccess)
            return Result<FinishConsultationResponse>.Fail(access.ErrorCode!, access.ErrorMessage!);

        var appointment = access.Value!.Appointment;
        var report = access.Value.Report;
        if (report is null)
            return Result<FinishConsultationResponse>.Fail(ErrorCodes.NotFound, "Relatório não encontrado.");

        if (report.Status is not (ConsultationStatus.Recording or ConsultationStatus.Interrupted))
            return Result<FinishConsultationResponse>.Fail(
                ErrorCodes.InvalidStatus, "A consulta não está gravando.");

        if (report.ChunkCount == 0)
            return Result<FinishConsultationResponse>.Fail(
                ErrorCodes.ConsultationEmpty, "Nenhum áudio foi enviado para esta consulta.");

        var now = time.GetUtcNow().UtcDateTime;
        report.Status = ConsultationStatus.Processing;
        report.EndedAt = now;
        report.NextAttemptAt = now;
        report.ProcessingAttempts = 0;
        report.UpdatedByUserId = usuario.UserId;

        // Encerra antes da cobrança: erro na cobrança não desfaz a consulta.
        await db.SaveChangesAsync(ct);

        int? paymentId = null;
        string? chargeError = null;

        if (dto.Charge is not null)
        {
            try
            {
                var timeZone = await ClinicTimeZoneAsync();
                var localDate = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(appointment.AppointmentDate, DateTimeKind.Utc), timeZone);
                var referenceMonth = new DateOnly(localDate.Year, localDate.Month, 1);

                var charge = await payments.CreateAsync(new CreatePaymentDto
                {
                    ResponsavelId = appointment.UserId,
                    PatientId = appointment.PatientId,
                    PlanId = dto.Charge.PlanId,
                    ReferenceMonth = referenceMonth,
                    PaymentMethod = dto.Charge.PaymentMethod,
                    Status = PaymentStatus.Pending,
                    SendOnline = dto.Charge.SendOnline,
                });

                if (!charge.IsSuccess)
                    chargeError = charge.ErrorMessage;
                else
                {
                    paymentId = charge.Value!.Id;

                    if (PaymentMethods.IsOnline(dto.Charge.PaymentMethod) && dto.Charge.SendOnline)
                    {
                        // O envio é acessório, como no PaymentsController: a cobrança já existe.
                        try
                        {
                            var sent = await onlinePayments.SendAsync(paymentId.Value, ct);
                            if (!sent.IsSuccess)
                                logger.LogError(
                                    "Falha ao enviar a cobrança {PaymentId} da consulta {AppointmentId}: {Code} {Message}",
                                    paymentId, appointment.Id, sent.ErrorCode, sent.ErrorMessage);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception,
                                "Falha ao enviar a cobrança {PaymentId} da consulta {AppointmentId}.",
                                paymentId, appointment.Id);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Falha ao criar a cobrança da consulta {AppointmentId}.", appointment.Id);
                chargeError = "Não foi possível gerar a cobrança.";
            }
        }

        return Result<FinishConsultationResponse>.Ok(new FinishConsultationResponse(
            report.Status.ToString(), paymentId, chargeError));
    }

    // ── Leitura e relatório ──────────────────────────────────────────────────

    public async Task<Result<ConsultationReportDto>> GetAsync(int appointmentId)
    {
        var access = await LoadAsync(appointmentId);
        if (!access.IsSuccess)
            return Result<ConsultationReportDto>.Fail(access.ErrorCode!, access.ErrorMessage!);

        return access.Value!.Report is null
            ? Result<ConsultationReportDto>.Fail(ErrorCodes.NotFound, "Relatório não encontrado.")
            : Result<ConsultationReportDto>.Ok(ToDto(access.Value.Report));
    }

    public async Task<Result<ConsultationReportDto>> UpdateReportAsync(
        int appointmentId, UpdateConsultationReportDto dto)
    {
        var access = await LoadAsync(appointmentId);
        if (!access.IsSuccess)
            return Result<ConsultationReportDto>.Fail(access.ErrorCode!, access.ErrorMessage!);

        var report = access.Value!.Report;
        if (report is null)
            return Result<ConsultationReportDto>.Fail(ErrorCodes.NotFound, "Relatório não encontrado.");

        if (report.Status != ConsultationStatus.Ready)
            return Result<ConsultationReportDto>.Fail(
                ErrorCodes.InvalidStatus, "O relatório só pode ser editado quando estiver pronto.");

        report.Subjective = Normalize(dto.Subjective);
        report.Objective = Normalize(dto.Objective);
        report.Assessment = Normalize(dto.Assessment);
        report.Plan = Normalize(dto.Plan);
        report.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();

        return Result<ConsultationReportDto>.Ok(ToDto(report));
    }

    public async Task<Result<ConsultationReportDto>> ReviewAsync(int appointmentId)
    {
        var access = await LoadAsync(appointmentId);
        if (!access.IsSuccess)
            return Result<ConsultationReportDto>.Fail(access.ErrorCode!, access.ErrorMessage!);

        var report = access.Value!.Report;
        if (report is null)
            return Result<ConsultationReportDto>.Fail(ErrorCodes.NotFound, "Relatório não encontrado.");

        if (report.Status != ConsultationStatus.Ready)
            return Result<ConsultationReportDto>.Fail(
                ErrorCodes.InvalidStatus, "Só é possível revisar um relatório pronto.");

        report.ReviewedAt = time.GetUtcNow().UtcDateTime;
        report.ReviewedByUserId = usuario.UserId;
        report.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();

        return Result<ConsultationReportDto>.Ok(ToDto(report));
    }

    public async Task<Result<ConsultationReportDto>> RetryAsync(int appointmentId)
    {
        var access = await LoadAsync(appointmentId);
        if (!access.IsSuccess)
            return Result<ConsultationReportDto>.Fail(access.ErrorCode!, access.ErrorMessage!);

        var report = access.Value!.Report;
        if (report is null)
            return Result<ConsultationReportDto>.Fail(ErrorCodes.NotFound, "Relatório não encontrado.");

        if (report.Status is ConsultationStatus.Failed or ConsultationStatus.Interrupted)
        {
            if (audio.ListChunks(report.Id).Count == 0)
                return Result<ConsultationReportDto>.Fail(
                    ErrorCodes.ConsultationEmpty, "Nenhum áudio foi enviado para esta consulta.");

            report.Status = ConsultationStatus.Processing;
            report.ProcessingAttempts = 0;
            report.NextAttemptAt = time.GetUtcNow().UtcDateTime;
            report.EndedAt ??= report.LastChunkAt ?? report.NextAttemptAt; // interrompida não tinha fim
            report.FailureReason = null;
        }
        else if (report.Status == ConsultationStatus.Ready && report.ReportPending)
            report.NextAttemptAt = time.GetUtcNow().UtcDateTime;
        else
            return Result<ConsultationReportDto>.Fail(
                ErrorCodes.InvalidStatus, "Não é possível tentar novamente nesta situação.");

        report.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();

        return Result<ConsultationReportDto>.Ok(ToDto(report));
    }

    // ── Cobrança, evolução e prontuário ──────────────────────────────────────

    public async Task<Result<ChargeSuggestionDto>> GetChargeSuggestionAsync(int appointmentId)
    {
        var access = await LoadAsync(appointmentId);
        if (!access.IsSuccess)
            return Result<ChargeSuggestionDto>.Fail(access.ErrorCode!, access.ErrorMessage!);

        var appointment = access.Value!.Appointment;
        var lastPayment = await db.Payments
            .AsNoTracking()
            .Include(p => p.Plan)
            .Where(p => p.ClinicaId == usuario.ClinicaId
                && p.PatientId == appointment.PatientId
                && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        var timeZone = await ClinicTimeZoneAsync();
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(appointment.AppointmentDate, DateTimeKind.Utc), timeZone);
        var monthStart = new DateOnly(localDate.Year, localDate.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var alreadyCharged = await db.Payments.AnyAsync(p =>
            p.ClinicaId == usuario.ClinicaId
            && p.PatientId == appointment.PatientId
            && !p.IsDeleted
            && p.ReferenceMonth >= monthStart
            && p.ReferenceMonth < monthEnd);

        var plans = await db.Plans.AsNoTracking()
            .Where(p => p.ClinicaId == usuario.ClinicaId && p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new ChargePlanOptionDto(p.Id, p.Name, p.TipoPlano.ToString(), p.Valor))
            .ToListAsync();

        // Mesma regra do PaymentService.ValidateOnlineChargeAsync.
        var clinicPlan = await db.Clinicas.Where(c => c.Id == usuario.ClinicaId).Select(c => c.Plan).SingleAsync();
        var onlineAvailable = PlanMatrix.Has(clinicPlan, Features.OnlinePayments)
            && await mercadoPagoAccounts.GetConnectedAsync(usuario.ClinicaId) is not null;

        return Result<ChargeSuggestionDto>.Ok(new ChargeSuggestionDto(
            lastPayment?.PlanId,
            lastPayment?.Plan.Name,
            lastPayment?.Plan.TipoPlano.ToString(),
            alreadyCharged,
            plans,
            onlineAvailable));
    }

    public async Task<Result<CreateEvolutionFromConsultationResponse>> CreateEvolutionAsync(
        int appointmentId, int treatmentId)
    {
        var access = await LoadAsync(appointmentId);
        if (!access.IsSuccess)
            return Result<CreateEvolutionFromConsultationResponse>.Fail(
                access.ErrorCode!, access.ErrorMessage!);

        var appointment = access.Value!.Appointment;
        var report = access.Value.Report;
        if (report is null)
            return Result<CreateEvolutionFromConsultationResponse>.Fail(
                ErrorCodes.NotFound, "Relatório não encontrado.");

        if (report.Status != ConsultationStatus.Ready)
            return Result<CreateEvolutionFromConsultationResponse>.Fail(
                ErrorCodes.InvalidStatus, "O relatório precisa estar pronto para gerar a evolução.");

        var treatment = await db.PatientTreatments
            .FirstOrDefaultAsync(t => t.Id == treatmentId
                && t.PatientId == appointment.PatientId
                && t.ClinicaId == usuario.ClinicaId
                && !t.IsDeleted);

        if (treatment is null)
            return Result<CreateEvolutionFromConsultationResponse>.Fail(
                ErrorCodes.NotFound, "Acompanhamento não encontrado.");

        var description = report.Subjective is null && report.Objective is null
            ? null
            : $"{report.Subjective}\n\n{report.Objective}";

        var evolution = new PatientEvolution
        {
            ClinicaId = usuario.ClinicaId,
            PatientId = appointment.PatientId,
            TreatmentId = treatment.Id,
            ProfessionalId = appointment.UserId,
            Date = DateTime.SpecifyKind(appointment.AppointmentDate, DateTimeKind.Utc),
            Description = description,
            Observations = report.Assessment,
            Conduct = report.Plan,
            Status = EvolutionStatus.Draft,
            CreatedByUserId = usuario.UserId,
        };
        db.PatientEvolutions.Add(evolution);
        await db.SaveChangesAsync();

        return Result<CreateEvolutionFromConsultationResponse>.Ok(
            new CreateEvolutionFromConsultationResponse(evolution.Id));
    }

    public async Task<Result<IReadOnlyList<ConsultationReportSummaryDto>>> GetPatientReportsAsync(
        int patientId)
    {
        var patientExists = await db.Patients.AnyAsync(p =>
            p.Id == patientId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);
        if (!patientExists)
            return Result<IReadOnlyList<ConsultationReportSummaryDto>>.Fail(
                ErrorCodes.NotFound, "Paciente não encontrado.");

        var query = db.ConsultationReports
            .AsNoTracking()
            .Where(r => r.ClinicaId == usuario.ClinicaId
                && r.PatientId == patientId
                && !r.IsDeleted);

        if (usuario.Role == UserRole.Profissional)
            query = query.Where(r => r.ProfessionalId == usuario.UserId);

        var rows = await query
            .OrderByDescending(r => r.Appointment.AppointmentDate)
            .Select(r => new
            {
                r.Id,
                r.AppointmentId,
                r.Appointment.AppointmentDate,
                ProfessionalName = r.Professional.Name,
                r.Status,
                r.ReviewedAt,
            })
            .ToListAsync();

        IReadOnlyList<ConsultationReportSummaryDto> summaries = rows
            .Select(r => new ConsultationReportSummaryDto(
                r.Id,
                r.AppointmentId,
                DateTime.SpecifyKind(r.AppointmentDate, DateTimeKind.Utc),
                r.ProfessionalName,
                r.Status.ToString(),
                r.ReviewedAt is not null))
            .ToList();

        return Result<IReadOnlyList<ConsultationReportSummaryDto>>.Ok(summaries);
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private async Task<TimeZoneInfo> ClinicTimeZoneAsync()
    {
        var timeZoneId = await db.Clinicas
            .Where(c => c.Id == usuario.ClinicaId)
            .Select(c => c.TimeZoneId)
            .FirstOrDefaultAsync();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ConsultationReportDto ToDto(ConsultationReport report) => new(
        report.Id,
        report.AppointmentId,
        report.PatientId,
        report.ProfessionalId,
        report.Status.ToString(),
        report.FailureReason,
        Utc(report.StartedAt),
        Utc(report.EndedAt),
        report.ChunkCount,
        report.DurationSeconds,
        report.Transcript,
        report.Subjective,
        report.Objective,
        report.Assessment,
        report.Plan,
        report.SuggestedReturnDays,
        report.ReportPending,
        report.MinutesCharged,
        Utc(report.ReviewedAt),
        report.ReviewedByUserId);

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value)
        => value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

    private sealed record Access(Appointment Appointment, ConsultationReport? Report);
}
