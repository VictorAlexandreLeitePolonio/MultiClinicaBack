using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.Agent;

public sealed class AgentWorker(AgentInboxProcessor processor, IOptions<AgentOptions> options,
    ILogger<AgentWorker> logger) : BackgroundService
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
                logger.LogError(exception, "Falha no ciclo do agente.");
            }
        }
    }
}
