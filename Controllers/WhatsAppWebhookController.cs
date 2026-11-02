using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Agent;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using MultiClinica.API.Services.Agent;

namespace MultiClinica.API.Controllers;

// Só enfileira: o LLM roda no AgentWorker para responder 200 rápido e não provocar reentrega.
[AllowAnonymous]
[ApiController]
[Route("webhooks/whatsapp")]
public class WhatsAppWebhookController(
    AppDbContext db,
    IWhatsAppClient whatsApp,
    WhatsAppLinkService links,
    UsageService usage,
    IOptions<WhatsAppOptions> options,
    TimeProvider time,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Verify([FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token, [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expected = options.Value.VerifyToken;
        if (mode != "subscribe" || string.IsNullOrEmpty(expected) || token is null
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expected)))
            return StatusCode(403);
        return Content(challenge ?? string.Empty, "text/plain");
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var body = buffer.ToArray();
        if (!HasValidSignature(body)) return Unauthorized();

        var payload = JsonSerializer.Deserialize<WhatsAppWebhookDto>(body) ?? new();
        var values = payload.Entry.SelectMany(e => e.Changes).Select(c => c.Value)
            .Where(v => v?.Metadata?.PhoneNumberId == options.Value.PhoneNumberId);
        foreach (var message in values.SelectMany(v => v!.Messages ?? []))
            await HandleAsync(message, ct);
        foreach (var status in values.SelectMany(v => v!.Statuses ?? []).Where(s => s.Status == "failed"))
            LogFailedStatus(status);
        // Sem try/catch de propósito: falha vira 500, a Meta reentrega o lote e o wamid descarta o que já foi gravado.
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    private void LogFailedStatus(WhatsAppStatusDto status)
    {
        // Sem errors no payload: loga código 0 para a falha não sumir.
        foreach (var error in status.Errors is { Count: > 0 } errors ? errors : [new WhatsAppStatusErrorDto()])
            logger.LogWarning("WhatsApp: entrega falhou {Wamid} código {ErrorCode} {ErrorTitle}",
                status.Id, error.Code, error.Title);
    }

    private bool HasValidSignature(byte[] body)
    {
        var secret = options.Value.AppSecret;
        if (string.IsNullOrEmpty(secret) || !Request.Headers.TryGetValue("X-Hub-Signature-256", out var header))
            return false;
        var expected = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(header.ToString()), Encoding.UTF8.GetBytes(expected));
    }

    private async Task HandleAsync(WhatsAppMessageDto message, CancellationToken ct)
    {
        var from = message.From;
        if (message.Type != "text")
        {
            await ReplyAsync(from, "Por enquanto eu só entendo texto. Me manda por escrito, por favor 🙏");
            return;
        }

        var text = message.Text?.Body.Trim() ?? "";
        if (text.StartsWith("conectar ", StringComparison.OrdinalIgnoreCase))
        {
            await links.CompleteAsync(from, text["conectar ".Length..].Trim());
            return;
        }

        var user = await db.Users.Include(u => u.Clinica)
            .FirstOrDefaultAsync(u => u.WhatsAppPhone == from && u.IsActive && !u.IsDeleted, ct);
        if (user is null)
        {
            await ReplyAsync(from, "Conecte sua conta pelo app: Configurações → Conectar WhatsApp.");
            return;
        }

        if (await db.AgentMessages.AnyAsync(m => m.Channel == AgentChannel.WhatsApp && m.ExternalMessageId == message.Id, ct)
            || db.AgentMessages.Local.Any(m => m.Channel == AgentChannel.WhatsApp && m.ExternalMessageId == message.Id))
            return;

        if (!PlanMatrix.Has(user.Clinica.Plan, Features.OwnerCopilot))
        {
            await ReplyAsync(from, "O copiloto está disponível a partir do plano Pro. Veja em Configurações → Plano.");
            return;
        }

        var now = time.GetUtcNow().UtcDateTime;
        var conversation = db.AgentConversations.Local.FirstOrDefault(c => c.Channel == AgentChannel.WhatsApp
                && c.ClinicaId == user.ClinicaId && c.ExternalChatId == from)
            ?? await db.AgentConversations.FirstOrDefaultAsync(c => c.Channel == AgentChannel.WhatsApp
                && c.ClinicaId == user.ClinicaId && c.ExternalChatId == from, ct);
        if (conversation is null || conversation.LastInboundAt < now.AddHours(-24))
        {
            if (!await usage.TryConsumeAsync(user.ClinicaId, UsageMetric.WhatsAppConversations, 1))
            {
                await ReplyAsync(from, "Sua cota de conversas do mês acabou. Compre um pacote em Configurações → Plano.");
                return;
            }
        }

        if (conversation is null)
        {
            conversation = new AgentConversation { ClinicaId = user.ClinicaId, Channel = AgentChannel.WhatsApp,
                Profile = AgentProfile.Owner, ExternalChatId = from };
            db.AgentConversations.Add(conversation);
        }
        conversation.UserId = user.Id;
        conversation.LastInboundAt = now;
        db.AgentMessages.Add(new AgentMessage { Conversation = conversation, Channel = AgentChannel.WhatsApp,
            Role = AgentMessageRole.User, Content = text, ExternalMessageId = message.Id, Status = AgentMessageStatus.Pending });
    }

    private async Task ReplyAsync(string to, string text)
    {
        try { await whatsApp.SendTextAsync(to, text, CancellationToken.None); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao responder no WhatsApp");
        }
    }
}
