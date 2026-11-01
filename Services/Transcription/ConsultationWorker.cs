using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.Transcription;

public sealed class ConsultationWorker(ConsultationProcessor processor, IOptions<TranscriptionOptions> options,
    ILogger<ConsultationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 1, 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(poll, stoppingToken);
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha no ciclo de consultas.");
            }
        }
    }
}
