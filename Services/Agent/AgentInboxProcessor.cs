using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Agent;

public sealed class AgentInboxProcessor(IServiceScopeFactory scopes, ILogger<AgentInboxProcessor> logger)
{
    public const int MaxAttempts = 3;
    public const string FailureReply = "Tive um problema para fazer isso agora. Tente de novo em alguns minutos ou use o app.";

    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        List<int> conversationIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            conversationIds = await db.AgentMessages
                .Where(m => m.Status == AgentMessageStatus.Pending)
                .GroupBy(m => m.ConversationId)
                .Select(g => new { g.Key, Oldest = g.Min(m => m.Id) })
                .OrderBy(x => x.Oldest).Take(20)
                .Select(x => x.Key)
                .ToListAsync(ct);
        }

        // ponytail: processamento sequencial em instância única; com várias instâncias, usar FOR UPDATE SKIP LOCKED.
        foreach (var id in conversationIds)
            await ProcessConversationAsync(id, ct);
    }

    private async Task ProcessConversationAsync(int conversationId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var telegram = sp.GetRequiredService<ITelegramClient>();
        var conversation = await db.AgentConversations.FirstAsync(c => c.Id == conversationId, ct);
        var chatId = long.Parse(conversation.ExternalChatId);

        var owner = await db.Users.Include(u => u.Clinica).FirstOrDefaultAsync(u => u.Id == conversation.UserId, ct);
        if (owner is null || !owner.IsActive || owner.IsDeleted || owner.TelegramChatId != chatId
            || !owner.Clinica.IsActive || owner.Clinica.IsBlockedByBilling)
        {
            await MarkPendingAsync(db, conversationId, AgentMessageStatus.Failed, ct);
            return;
        }

        sp.GetRequiredService<AgentIdentity>().Set(owner.ClinicaId, owner.Id, owner.Role);
        try
        {
            var reply = await sp.GetRequiredService<AgentRunner>().RunAsync(conversation, owner, ct);
            await MarkPendingAsync(db, conversationId, AgentMessageStatus.Done, ct);
            // ponytail: falha no envio só é logada (resposta já gravada); retentar envio se virar problema real.
            await SendSafelyAsync(telegram, chatId, reply, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Falha ao processar conversa {ConversationId}", conversationId);
            db.ChangeTracker.Clear();
            var pending = await db.AgentMessages
                .Where(m => m.ConversationId == conversationId && m.Status == AgentMessageStatus.Pending)
                .ToListAsync(ct);
            foreach (var message in pending) message.Attempts++;
            var givingUp = pending.Any(m => m.Attempts >= MaxAttempts);
            if (givingUp)
                foreach (var message in pending) message.Status = AgentMessageStatus.Failed;
            await db.SaveChangesAsync(ct);
            if (givingUp)
                await SendSafelyAsync(telegram, chatId, FailureReply, ct);
        }
    }

    private static async Task MarkPendingAsync(AppDbContext db, int conversationId, AgentMessageStatus status,
        CancellationToken ct)
    {
        var pending = await db.AgentMessages
            .Where(m => m.ConversationId == conversationId && m.Status == AgentMessageStatus.Pending)
            .ToListAsync(ct);
        foreach (var message in pending) message.Status = status;
        await db.SaveChangesAsync(ct);
    }

    private async Task SendSafelyAsync(ITelegramClient telegram, long chatId, string text, CancellationToken ct)
    {
        try { await telegram.SendMessageAsync(chatId, text, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Falha ao enviar mensagem ao Telegram"); }
    }
}
