using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;

namespace ProjetoLP.API.Services;

// Serviço em segundo plano que envia lembretes de consulta via WhatsApp.
// Executa ao iniciar a aplicação e depois a cada hora.
// Só envia para consultas Scheduled com data entre 23h e 25h a partir de agora,
// e que ainda não receberam lembrete (ReminderSent = false).
public class AppointmentReminderJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<AppointmentReminderJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public AppointmentReminderJob(
        IServiceScopeFactory scopeFactory,
        IWhatsAppService whatsApp,
        ILogger<AppointmentReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _whatsApp     = whatsApp;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SendRemindersAsync();
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task SendRemindersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now  = DateTime.UtcNow;
        var from = now.AddHours(23);
        var to   = now.AddHours(25);

        var appointments = await db.Appointments
            .Include(a => a.Patient)
            .Where(a =>
                a.Status       == AppointmentStatus.Scheduled &&
                a.ReminderSent == false                       &&
                a.AppointmentDate >= from                     &&
                a.AppointmentDate <= to)
            .ToListAsync();

        if (appointments.Count == 0) return;

        foreach (var appointment in appointments)
        {
            var patient = appointment.Patient;
            var date    = appointment.AppointmentDate.ToLocalTime();
            var message =
                $"Olá, {patient.Name}! 😊\n" +
                $"Lembramos que você tem uma consulta amanhã às {date:HH:mm}.\n" +
                $"Caso precise remarcar, entre em contato com a clínica com antecedência.\n" +
                $"Até lá!";

            string? errorMessage = null;
            bool    success      = false;

            try
            {
                success = await _whatsApp.SendTextAsync(patient.Phone, message);
                if (success)
                {
                    appointment.ReminderSent = true;
                    _logger.LogInformation(
                        "[AppointmentReminderJob] Lembrete enviado para {Patient} ({Phone}) — consulta em {Date}.",
                        patient.Name, patient.Phone, date);
                }
                else
                {
                    errorMessage = "A Evolution API retornou status de falha.";
                    _logger.LogWarning(
                        "[AppointmentReminderJob] Falha ao enviar lembrete para {Patient} ({Phone}).",
                        patient.Name, patient.Phone);
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _logger.LogError(ex,
                    "[AppointmentReminderJob] Erro ao enviar lembrete para {Patient} ({Phone}).",
                    patient.Name, patient.Phone);
            }

            db.WhatsAppLogs.Add(new WhatsAppLog
            {
                PatientId    = patient.Id,
                Phone        = patient.Phone,
                Message      = message,
                Type         = "AppointmentReminder",
                Success      = success,
                ErrorMessage = errorMessage,
                SentAt       = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }
}
