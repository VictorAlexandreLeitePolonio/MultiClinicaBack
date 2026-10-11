using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Agent;

public sealed class AgentInboxProcessor(IServiceScopeFactory scopes, ILogger<AgentInboxProcessor> logger, TimeProvider time)
{
    public const int MaxAttempts = 3;
    public const string FailureReply = "Tive um problema para fazer isso agora. Tente de novo em alguns minutos ou use o app.";

    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        List<int> conversationIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // ponytail: backoff linear 30s×tentativas via UpdatedAt; usar NextAttemptAt se precisar de política melhor.
            var pending = await db.AgentMessages
                .Where(m => m.Status == AgentMessageStatus.Pending)
                .Select(m => new { m.Id, m.ConversationId, m.Attempts, m.UpdatedAt })
                .ToListAsync(ct);
            conversationIds = pending
                .Where(m => IsDue(m.Attempts, m.UpdatedAt, now))
                .GroupBy(m => m.ConversationId)
                .OrderBy(g => g.Min(m => m.Id)).Take(20)
                .Select(g => g.Key)
                .ToList();
        }

        // ponytail: processamento sequencial em instância única; com várias instâncias, usar FOR UPDATE SKIP LOCKED.
        foreach (var id in conversationIds)
        {
            try
            {
                await ProcessConversationAsync(id, now, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha ao processar conversa {ConversationId}", id);
            }
        }
    }

    private async Task ProcessConversationAsync(int conversationId, DateTime now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var telegram = sp.GetRequiredService<ITelegramClient>();
        var conversation = await db.AgentConversations.FirstAsync(c => c.Id == conversationId, ct);
        var chatId = long.Parse(conversation.ExternalChatId);

        // Snapshot antes do LLM: só estas mensagens são marcadas; o que chegar durante a chamada fica Pending para o próximo ciclo.
        var batch = (await db.AgentMessages
                .Where(m => m.ConversationId == conversationId && m.Status == AgentMessageStatus.Pending)
                .Select(m => new { m.Id, m.Attempts, m.UpdatedAt })
                .ToListAsync(ct))
            .Where(m => IsDue(m.Attempts, m.UpdatedAt, now))
            .Select(m => m.Id)
            .ToList();
        if (batch.Count == 0) return;

        var owner = await db.Users.Include(u => u.Clinica).FirstOrDefaultAsync(u => u.Id == conversation.UserId, ct);
        if (owner is null || !owner.IsActive || owner.IsDeleted || owner.Role != UserRole.Administrador
            || owner.TelegramChatId != chatId || !owner.Clinica.IsActive || owner.Clinica.IsBlockedByBilling)
        {
            await SetStatusAsync(db, batch, AgentMessageStatus.Failed, ct);
            return;
        }

        sp.GetRequiredService<AgentIdentity>().Set(owner.ClinicaId, owner.Id, owner.Role);
        try
        {
            var reply = await sp.GetRequiredService<AgentRunner>().RunAsync(conversation, owner, ct);
            await SetStatusAsync(db, batch, AgentMessageStatus.Done, ct);
            // ponytail: falha no envio só é logada (resposta já gravada); retentar envio se virar problema real.
            await SendSafelyAsync(telegram, chatId, reply, ct);
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
                await SendSafelyAsync(telegram, chatId, FailureReply, ct);
        }
    }

    // Sem tentativa: já é elegível. Depois da n-ésima falha, espera 30s×n desde a última gravação (UpdatedAt).
    private static bool IsDue(int attempts, DateTime updatedAt, DateTime now)
        => attempts == 0 || updatedAt <= now - TimeSpan.FromSeconds(30 * attempts);

    private static async Task SetStatusAsync(AppDbContext db, List<int> ids, AgentMessageStatus status, CancellationToken ct)
    {
        var messages = await db.AgentMessages
            .Where(m => ids.Contains(m.Id) && m.Status == AgentMessageStatus.Pending)
            .ToListAsync(ct);
        foreach (var message in messages) message.Status = status;
        await db.SaveChangesAsync(ct);
    }

    private async Task SendSafelyAsync(ITelegramClient telegram, long chatId, string text, CancellationToken ct)
    {
        try { await telegram.SendMessageAsync(chatId, text, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Falha ao enviar mensagem ao Telegram"); }
    }
}
