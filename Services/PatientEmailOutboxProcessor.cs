using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class PatientEmailOutboxProcessor(
    AppDbContext db,
    IPatientNotificationService notifications,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<PatientEmailOutboxProcessor> logger)
{
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var maxAttempts = Math.Clamp(configuration.GetValue("PatientEmailOutbox:MaxAttempts", 5), 1, 20);
        var batchSize = Math.Clamp(configuration.GetValue("PatientEmailOutbox:BatchSize", 20), 1, 100);

        await db.PatientEmailOutbox
            .Where(job => job.Status == PatientEmailOutboxStatus.Pending
                && job.AttemptCount >= maxAttempts
                && (job.LeaseUntil == null || job.LeaseUntil <= now))
            .ExecuteUpdateAsync(update => update
                .SetProperty(job => job.Status, PatientEmailOutboxStatus.Failed)
                .SetProperty(job => job.LastError, "Limite de tentativas excedido.")
                .SetProperty(job => job.LeaseToken, (Guid?)null)
                .SetProperty(job => job.LeaseUntil, (DateTime?)null), cancellationToken);

        var dueJobIds = await db.PatientEmailOutbox
            .Where(job => job.Status == PatientEmailOutboxStatus.Pending
                && job.AttemptCount < maxAttempts
                && job.NextAttemptAt <= now
                && (job.LeaseUntil == null || job.LeaseUntil <= now))
            .OrderBy(job => job.NextAttemptAt)
            .ThenBy(job => job.Id)
            .Select(job => job.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var jobId in dueJobIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var leaseToken = Guid.NewGuid();
            var leaseUntil = now.AddSeconds(Math.Clamp(configuration.GetValue("PatientEmailOutbox:LeaseSeconds", 60), 10, 600));
            var claimed = await db.PatientEmailOutbox
                .Where(job => job.Id == jobId
                    && job.Status == PatientEmailOutboxStatus.Pending
                    && job.AttemptCount < maxAttempts
                    && job.NextAttemptAt <= now
                    && (job.LeaseUntil == null || job.LeaseUntil <= now))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(job => job.LeaseToken, leaseToken)
                    .SetProperty(job => job.LeaseUntil, leaseUntil)
                    .SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1), cancellationToken);
            if (claimed == 0)
                continue;

            var job = await db.PatientEmailOutbox
                .Include(item => item.Patient)
                .Include(item => item.PatientAccount)
                .Include(item => item.Clinica)
                .SingleOrDefaultAsync(item => item.Id == jobId && item.LeaseToken == leaseToken, cancellationToken);
            if (job is null)
                continue;

            var sent = false;
            var failureMessage = "Falha no envio da notificação.";
            try
            {
                if (job.Type == PatientEmailOutboxType.PaymentCharge)
                {
                    failureMessage = "Falha no envio da cobrança.";
                    var payment = job.PaymentId is { } paymentId
                        ? await db.Payments
                            .Include(item => item.Plan)
                            .FirstOrDefaultAsync(item => item.Id == paymentId, cancellationToken)
                        : null;
                    if (payment?.PaymentLinkToken is null)
                    {
                        failureMessage = "A cobrança não está disponível.";
                    }
                    else
                    {
                        var clinicName = string.IsNullOrWhiteSpace(job.Clinica.NomeFantasia)
                            ? job.Clinica.Nome
                            : job.Clinica.NomeFantasia;
                        var frontend = (configuration["FRONTEND_URL"] ?? "https://cliniqcare.com.br").TrimEnd('/');
                        sent = await notifications.SendPaymentChargeAsync(job.Patient, payment, clinicName,
                            $"{frontend}/pagar/{payment.PaymentLinkToken}");
                    }
                }
                else if (job.PatientAccount is { IsDeleted: false } pendingAccount
                    && pendingAccount.Status == PatientAccountStatus.PendingActivation)
                {
                    job.Type = PatientEmailOutboxType.ActivationInvitation;
                    sent = await notifications.SendActivationInviteAsync(pendingAccount);
                }
                else if (job.PatientAccount is { IsDeleted: false } account)
                {
                    job.Type = PatientEmailOutboxType.ClinicLinkNotice;
                    var clinicName = string.IsNullOrWhiteSpace(job.Clinica.NomeFantasia)
                        ? job.Clinica.Nome
                        : job.Clinica.NomeFantasia;
                    sent = await notifications.SendNewLinkNoticeAsync(account, clinicName);
                }
                else
                {
                    failureMessage = "A conta do paciente não está disponível.";
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Falha no envio do job de e-mail do paciente {JobId}, tentativa {AttemptCount}.",
                    job.Id, job.AttemptCount);
            }

            var finishedAt = timeProvider.GetUtcNow().UtcDateTime;
            if (sent)
            {
                job.Status = PatientEmailOutboxStatus.Completed;
                job.CompletedAt = finishedAt;
                job.LastError = null;
            }
            else
            {
                job.Status = job.AttemptCount >= maxAttempts
                    ? PatientEmailOutboxStatus.Failed
                    : PatientEmailOutboxStatus.Pending;
                job.NextAttemptAt = finishedAt.AddSeconds(RetryDelaySeconds(job.AttemptCount));
                job.LastError = failureMessage;
                logger.LogWarning("Job de e-mail do paciente {JobId} falhou na tentativa {AttemptCount}.",
                    job.Id, job.AttemptCount);
            }

            job.LeaseToken = null;
            job.LeaseUntil = null;
            await db.SaveChangesAsync(cancellationToken);
            processed++;
        }

        return processed;
    }

    private int RetryDelaySeconds(int attemptCount)
    {
        var baseSeconds = Math.Clamp(configuration.GetValue("PatientEmailOutbox:RetryBaseSeconds", 30), 1, 3600);
        var exponent = Math.Clamp(attemptCount - 1, 0, 10);
        return (int)Math.Min(3600, baseSeconds * Math.Pow(2, exponent));
    }
}
