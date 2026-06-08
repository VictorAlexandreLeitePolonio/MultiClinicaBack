using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services;

// Serviço em segundo plano que cancela automaticamente consultas agendadas no passado.
// Executa ao iniciar a aplicação e depois a cada hora.
public class AppointmentStatusUpdater : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentStatusUpdater> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public AppointmentStatusUpdater(IServiceScopeFactory scopeFactory, ILogger<AppointmentStatusUpdater> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CancelPastAppointmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentStatusUpdater] Falha ao cancelar consultas passadas.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CancelPastAppointmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        var outdated = await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Scheduled && a.AppointmentDate < now)
            .ToListAsync(cancellationToken);

        if (outdated.Count == 0) return;

        foreach (var appointment in outdated)
            appointment.Status = AppointmentStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[AppointmentStatusUpdater] {Count} consulta(s) cancelada(s) automaticamente em {Time}.",
            outdated.Count,
            now);
    }
}
