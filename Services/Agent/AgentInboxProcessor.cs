using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Agent;

public sealed class AgentInboxProcessor(IServiceScopeFactory scopes, ILogger<AgentInboxProcessor> logger, TimeProvider time)
{
    public const int MaxAttempts = 3;
    public const string FailureReply = "Tive um problema para fazer isso agora. Tente de novo em alguns minutos ou use o app.";
    // O WhatsApp aceita 4096 caracteres por mensagem; 4095 + "…" fica no limite.
    private const int WhatsAppReplyMaxChars = 4095;

    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        // ponytail: backoff via UpdatedAt: 1 falha espera 30s, 2 ou mais espera 60s (MaxAttempts 3); usar NextAttemptAt se precisar de política melhor.
        var t30 = now.AddSeconds(-30);
        var t60 = now.AddSeconds(-60);
        List<int> conversationIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            conversationIds = await db.AgentMessages
                .Where(m => m.Status == AgentMessageStatus.Pending
                    && (m.Attempts == 0 || (m.Attempts == 1 && m.UpdatedAt <= t30) || (m.Attempts >= 2 && m.UpdatedAt <= t60)))
                .GroupBy(m => m.ConversationId)
                .OrderBy(g => g.Min(m => m.Id)).Take(20)
                .Select(g => g.Key)
                .ToListAsync(ct);
        }

        // ponytail: instância única obrigatória enquanto não houver FOR UPDATE SKIP LOCKED/advisory lock; com Agent:Enabled em 2+ instâncias haveria respostas e ações duplicadas.
        foreach (var id in conversationIds)
        {
            try
            {
                await ProcessConversationAsync(id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha ao processar conversa {ConversationId}", id);
            }
        }
    }

    private async Task ProcessConversationAsync(int conversationId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var whatsApp = sp.GetRequiredService<IWhatsAppClient>();
        var conversation = await db.AgentConversations.FirstAsync(c => c.Id == conversationId, ct);

        // Snapshot antes do LLM: TODAS as Pending da conversa, porque o histórico enviado ao modelo traz as Pending (LoadHistoryAsync).
        // O que chegar durante a chamada fica Pending (Attempts 0) para o próximo ciclo.
        var batch = await db.AgentMessages
            .Where(m => m.ConversationId == conversationId && m.Status == AgentMessageStatus.Pending)
            .Select(m => m.Id)
            .ToListAsync(ct);
        if (batch.Count == 0) return;

        var to = conversation.ExternalChatId;
        var owner = await db.Users.Include(u => u.Clinica).FirstOrDefaultAsync(u => u.Id == conversation.UserId, ct);
        if (owner is null || !owner.IsActive || owner.IsDeleted || owner.Role != UserRole.Administrador
            || owner.WhatsAppPhone != to || !owner.Clinica.IsActive || owner.Clinica.IsBlockedByBilling
            || !PlanMatrix.Has(owner.Clinica.Plan, Features.OwnerCopilot)
            || owner.Clinica.IsDeleted)
        {
            await SetStatusAsync(db, batch, AgentMessageStatus.Failed, ct);
            return;
        }

        sp.GetRequiredService<AgentIdentity>().Set(owner.ClinicaId, owner.Id, owner.Role);
        try
        {
            var reply = await sp.GetRequiredService<AgentRunner>().RunAsync(conversation, owner, batch.Max(), ct);
            await SetStatusAsync(db, batch, AgentMessageStatus.Done, ct);
            // ponytail: falha no envio só é logada (resposta já gravada); retentar envio se virar problema real. A ação já foi executada; o histórico guarda a resposta. Reenvio exigiria marcador de não-enviado no AgentMessage.
            await SendSafelyAsync(whatsApp, to, TruncateReply(reply), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Falha ao processar conversa {ConversationId}", conversationId);
            db.ChangeTracker.Clear();
            var failed = await db.AgentMessages
                .Where(m => batch.Contains(m.Id) && m.Status == AgentMessageStatus.Pending)
                .ToListAsync(ct);
            var gaveUp = false;
            foreach (var message in failed)
            {
                message.Attempts++;
                if (message.Attempts < MaxAttempts) continue;
                message.Status = AgentMessageStatus.Failed;
                gaveUp = true;
            }
            await db.SaveChangesAsync(ct);
            if (gaveUp)
                await SendSafelyAsync(whatsApp, to, FailureReply, ct);
        }
    }

    private static async Task SetStatusAsync(AppDbContext db, List<int> ids, AgentMessageStatus status, CancellationToken ct)
    {
        var messages = await db.AgentMessages
            .Where(m => ids.Contains(m.Id) && m.Status == AgentMessageStatus.Pending)
            .ToListAsync(ct);
        foreach (var message in messages) message.Status = status;
        await db.SaveChangesAsync(ct);
    }

    // Corta sem deixar um par substituto (emoji) partido no fim.
    private static string TruncateReply(string text)
    {
        if (text.Length <= WhatsAppReplyMaxChars) return text;
        var cut = char.IsHighSurrogate(text[WhatsAppReplyMaxChars - 1]) ? WhatsAppReplyMaxChars - 1 : WhatsAppReplyMaxChars;
        return text[..cut] + "…";
    }

    private async Task SendSafelyAsync(IWhatsAppClient whatsApp, string to, string text, CancellationToken ct)
    {
        try { await whatsApp.SendTextAsync(to, text, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Falha ao enviar mensagem no WhatsApp"); }
    }
}
