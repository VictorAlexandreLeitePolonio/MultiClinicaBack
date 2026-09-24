using Microsoft.Extensions.DependencyInjection;

namespace MultiClinica.API.Services;

public sealed class PatientEmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PatientEmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollSeconds = Math.Clamp(configuration.GetValue("PatientEmailOutbox:PollSeconds", 10), 1, 300);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<PatientEmailOutboxProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao processar a fila de e-mails de pacientes.");
            }
        }
    }
}
