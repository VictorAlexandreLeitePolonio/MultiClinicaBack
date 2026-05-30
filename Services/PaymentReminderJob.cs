using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;

namespace ProjetoLP.API.Services;

// Serviço em segundo plano que envia lembretes de vencimento de pagamento via WhatsApp.
// Executa ao iniciar a aplicação e depois a cada hora.
// Só envia para pagamentos Pending com PaymentDate entre 23h e 25h a partir de agora,
// e que ainda não receberam lembrete (PaymentReminderSent = false).
public class PaymentReminderJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<PaymentReminderJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public PaymentReminderJob(
        IServiceScopeFactory scopeFactory,
        IWhatsAppService whatsApp,
        ILogger<PaymentReminderJob> logger)
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

        var payments = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .Where(p =>
                p.Status               == PaymentStatus.Pending &&
                p.PaymentReminderSent  == false                 &&
                p.PaymentDate.HasValue                          &&
                p.PaymentDate.Value >= from                     &&
                p.PaymentDate.Value <= to)
            .ToListAsync();

        if (payments.Count == 0) return;

        foreach (var payment in payments)
        {
            var patient     = payment.Patient;
            var dueDate     = payment.PaymentDate!.Value.ToLocalTime();
            var message     =
                $"Olá, {patient.Name}! 😊\n" +
                $"Lembramos que o seu pagamento referente a *{payment.Plan.Name}* " +
                $"({payment.ReferenceMonth}) vence amanhã ({dueDate:dd/MM/yyyy}).\n" +
                $"Valor: R$ {payment.Amount:N2}.\n" +
                $"Entre em contato com a clínica para regularizar. Obrigado!";

            string? errorMessage = null;
            bool    success      = false;

            try
            {
                success = await _whatsApp.SendTextAsync(patient.Phone, message);
                if (success)
                {
                    payment.PaymentReminderSent = true;
                    _logger.LogInformation(
                        "[PaymentReminderJob] Lembrete enviado para {Patient} ({Phone}) — vencimento em {Date}.",
                        patient.Name, patient.Phone, dueDate);
                }
                else
                {
                    errorMessage = "A Evolution API retornou status de falha.";
                    _logger.LogWarning(
                        "[PaymentReminderJob] Falha ao enviar lembrete para {Patient} ({Phone}).",
                        patient.Name, patient.Phone);
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _logger.LogError(ex,
                    "[PaymentReminderJob] Erro ao enviar lembrete para {Patient} ({Phone}).",
                    patient.Name, patient.Phone);
            }

            db.WhatsAppLogs.Add(new WhatsAppLog
            {
                PatientId    = patient.Id,
                Phone        = patient.Phone,
                Message      = message,
                Type         = "PaymentReminder",
                Success      = success,
                ErrorMessage = errorMessage,
                SentAt       = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }
}
