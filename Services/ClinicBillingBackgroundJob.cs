using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class ClinicBillingBackgroundJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ClinicBillingBackgroundJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IClinicaBillingService>();
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                await service.GenerateMonthlyChargesAsync(today, stoppingToken);
                await service.BlockOverdueClinicsAsync(today, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ClinicBillingBackgroundJob] Falha ao processar cobranças.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
