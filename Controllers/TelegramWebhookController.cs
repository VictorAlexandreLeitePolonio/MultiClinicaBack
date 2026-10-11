using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Agent;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;

namespace MultiClinica.API.Controllers;

// Só enfileira: o LLM roda no AgentWorker para responder 200 rápido e não provocar reentrega.
[AllowAnonymous]
[ApiController]
[Route("webhooks/telegram")]
public class TelegramWebhookController(
    AppDbContext db,
    ITelegramClient telegram,
    TelegramLinkService links,
    IOptions<TelegramOptions> options,
    TimeProvider time,
    ILogger<TelegramWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] TelegramUpdateDto update, CancellationToken ct)
    {
        if (!HasValidSecret()) return Unauthorized();

        var message = update.Message;
        if (message is null) return Ok();
        var chatId = message.Chat.Id;
        var text = message.Text?.Trim();

        if (text is not null && (text == "/start" || text.StartsWith("/start ", StringComparison.Ordinal)))
        {
            var code = text["/start".Length..].Trim();
            if (code.Length > 0) await links.CompleteAsync(chatId, code);
            else await ReplyAsync(chatId, "Para conectar, use o botão Conectar Telegram nas Configurações do app.", ct);
            return Ok();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId && u.IsActive && !u.IsDeleted, ct);
        if (user is null)
        {
            await ReplyAsync(chatId, "Conecte sua conta pelo app: Configurações → Conectar Telegram.", ct);
            return Ok();
        }
        if (string.IsNullOrEmpty(text))
        {
            await ReplyAsync(chatId, "Por enquanto eu só entendo texto. Me manda por escrito, por favor 🙏", ct);
            return Ok();
        }

        var externalId = $"{chatId}:{message.MessageId}";
        if (await db.AgentMessages.AnyAsync(m => m.Channel == AgentChannel.Telegram && m.ExternalMessageId == externalId, ct))
            return Ok();

        var chatKey = chatId.ToString();
        var conversation = await db.AgentConversations.FirstOrDefaultAsync(c => c.Channel == AgentChannel.Telegram
            && c.ClinicaId == user.ClinicaId && c.ExternalChatId == chatKey, ct);
        if (conversation is null)
        {
            conversation = new AgentConversation { ClinicaId = user.ClinicaId, Channel = AgentChannel.Telegram,
                Profile = AgentProfile.Owner, ExternalChatId = chatKey };
            db.AgentConversations.Add(conversation);
        }
        conversation.UserId = user.Id;
        conversation.LastInboundAt = time.GetUtcNow().UtcDateTime;
        db.AgentMessages.Add(new AgentMessage { Conversation = conversation, Channel = AgentChannel.Telegram,
            Role = AgentMessageRole.User, Content = text, ExternalMessageId = externalId, Status = AgentMessageStatus.Pending });

        // Sem try/catch de propósito. Engolir DbUpdateException devolveria 200 e o Telegram nunca reentregaria: mensagem perdida.
        // Sem engolir, uma reentrega concorrente que bate no índice único vira 500, o Telegram reenvia e o AnyAsync acima responde 200.
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    private bool HasValidSecret()
    {
        var expected = options.Value.WebhookSecret;
        if (string.IsNullOrEmpty(expected)
            || !Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var received))
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(received.ToString()), Encoding.UTF8.GetBytes(expected));
    }

    private async Task ReplyAsync(long chatId, string text, CancellationToken ct)
    {
        try { await telegram.SendMessageAsync(chatId, text, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Falha ao responder no Telegram"); }
    }
}
