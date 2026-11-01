using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;

namespace MultiClinica.API.Services.Transcription;

// Processa as consultas gravadas fora do request: interrompe inativas, transcreve,
// gera o relatório SOAP e apaga áudio antigo. Instância única (mesma nota do agente).
public sealed class ConsultationProcessor(
    IServiceScopeFactory scopes,
    ITranscriber transcriber,
    IChatModel chat,
    ConsultationAudioStore audio,
    IOptions<AgentOptions> agent,
    TimeProvider time,
    ILogger<ConsultationProcessor> logger)
{
    private const int MaxReportsPerBatch = 5;
    private const int MaxTranscriptionAttempts = 3;
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AudioRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan ReportRetryDelay = TimeSpan.FromMinutes(30);

    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await InterruptInactiveAsync(now, ct);
        await ProcessDueReportsAsync(now, ct);
        await CleanupOldAudioAsync(now, ct);
    }

    private async Task InterruptInactiveAsync(DateTime now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = now - InactivityTimeout;
        var inactive = await db.ConsultationReports
            .Where(r => r.Status == ConsultationStatus.Recording
                && (r.LastChunkAt ?? r.StartedAt) < cutoff)
            .ToListAsync(ct);

        if (inactive.Count == 0) return;
        foreach (var report in inactive)
        {
            report.Status = ConsultationStatus.Interrupted;
            logger.LogInformation("Relatório {ReportId}: interrompido por inatividade.", report.Id);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task ProcessDueReportsAsync(DateTime now, CancellationToken ct)
    {
        List<int> ids;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ids = await db.ConsultationReports
                .Where(r => (r.Status == ConsultationStatus.Processing
                        || (r.Status == ConsultationStatus.Ready && r.ReportPending))
                    && r.NextAttemptAt != null && r.NextAttemptAt <= now)
                .OrderBy(r => r.NextAttemptAt)
                .Take(MaxReportsPerBatch)
                .Select(r => r.Id)
                .ToListAsync(ct);
        }

        foreach (var id in ids)
        {
            try
            {
                await ProcessReportAsync(id, now, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Sem transcrição nem relatório no log.
                logger.LogError("Falha inesperada ao processar o relatório {ReportId}.", id);
            }
        }
    }

    private async Task ProcessReportAsync(int reportId, DateTime now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var report = await db.ConsultationReports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null) return;

        // Transcrição só uma vez; depois o relatório pendente é refeito sem reouvir o áudio.
        // Transcript vazio ("") também conta como transcrito: o áudio já foi apagado e os minutos cobrados.
        if (report.Transcript is null
            && !await TryTranscribeAsync(report, scope.ServiceProvider, now, ct))
            return;

        await GenerateReportAsync(report, db, now, ct);
    }

    private async Task<bool> TryTranscribeAsync(ConsultationReport report, IServiceProvider services,
        DateTime now, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        string? temp = null;
        try
        {
            temp = await audio.ConcatToTempFileAsync(report.Id, ct);
            await using var stream = File.OpenRead(temp);
            var result = await transcriber.TranscribeAsync(stream, "audio.webm", ct);

            report.Transcript = result.Text;
            report.DurationSeconds = result.DurationSeconds;
            report.MinutesCharged = Math.Max(1, (int)Math.Ceiling(result.DurationSeconds / 60d));
            await services.GetRequiredService<UsageService>().ConsumeAllowingOverageAsync(
                report.ClinicaId, UsageMetric.TranscriptionMinutes, report.MinutesCharged);
            await db.SaveChangesAsync(ct);

            try { audio.Delete(report.Id); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Relatório {ReportId}: falha ao apagar o áudio.", report.Id);
            }
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            report.ProcessingAttempts++;
            if (report.ProcessingAttempts >= MaxTranscriptionAttempts)
            {
                report.Status = ConsultationStatus.Failed;
                report.FailureReason = exception is TranscriptionNotConfiguredException
                    ? "Transcrição não configurada"
                    : "Transcrição indisponível";
                report.NextAttemptAt = null;
                report.ReportPending = false;
            }
            else
            {
                report.NextAttemptAt = now + (report.ProcessingAttempts switch
                {
                    1 => TimeSpan.FromMinutes(1),
                    2 => TimeSpan.FromMinutes(5),
                    _ => TimeSpan.FromMinutes(30),
                });
            }
            await db.SaveChangesAsync(ct);
            logger.LogWarning(
                "Relatório {ReportId}: falha na transcrição; tentativa {Attempt}; status {Status}.",
                report.Id, report.ProcessingAttempts, report.Status);
            return false;
        }
        finally
        {
            if (temp is not null)
            {
                try { File.Delete(temp); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    private async Task GenerateReportAsync(ConsultationReport report, AppDbContext db, DateTime now, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agent.Value.ApiKey))
                throw new InvalidOperationException("IA não configurada.");

            var result = await chat.CompleteAsync(agent.Value.Model,
                [
                    new AgentChatMessage(AgentChatRole.System, ConsultationPrompts.SoapSystem),
                    new AgentChatMessage(AgentChatRole.User, report.Transcript ?? string.Empty),
                ], [], ct);
            var soap = ConsultationPrompts.Parse(result.Content)
                ?? throw new InvalidOperationException("Relatório inválido.");

            report.Subjective = soap.Subjective;
            report.Objective = soap.Objective;
            report.Assessment = soap.Assessment;
            report.Plan = soap.Plan;
            report.SuggestedReturnDays = soap.SuggestedReturnDays;
            report.Status = ConsultationStatus.Ready;
            report.ReportPending = false;
            report.FailureReason = null;
            report.NextAttemptAt = null;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Relatório {ReportId}: pronto.", report.Id);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // A transcrição já está salva; a IA tenta de novo a cada 30 min, sem limite.
            report.Status = ConsultationStatus.Ready;
            report.ReportPending = true;
            report.NextAttemptAt = now + ReportRetryDelay;
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Relatório {ReportId}: aguardando a IA.", report.Id);
        }
    }

    private async Task CleanupOldAudioAsync(DateTime now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = now - AudioRetention;
        var ids = await db.ConsultationReports
            .Where(r => (r.Status == ConsultationStatus.Failed || r.Status == ConsultationStatus.Interrupted)
                && r.UpdatedAt < cutoff)
            .Select(r => r.Id)
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            try
            {
                audio.Delete(id);
                logger.LogInformation("Relatório {ReportId}: áudio apagado após retenção.", id);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Relatório {ReportId}: falha ao apagar o áudio antigo.", id);
            }
        }
    }
}
