using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.MercadoPago;
using Npgsql;

namespace MultiClinica.API.Controllers;

// Webhook de pagamentos do Mercado Pago. A assinatura é a única autenticação; o corpo
// nunca é fonte de verdade — sempre consultamos a API pelo data.id.
[ApiController]
[Route("api/payments/webhooks")]
[AllowAnonymous]
public class MercadoPagoWebhookController(
    AppDbContext db,
    MercadoPagoPaymentSync sync,
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoWebhookController> logger) : ControllerBase
{
    private const string Provider = "mercadopago";

    [HttpPost("mercadopago")]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        var dataId = Request.Query["data.id"].FirstOrDefault();
        var requestId = Request.Headers["x-request-id"].FirstOrDefault() ?? string.Empty;
        var signature = Request.Headers["x-signature"].FirstOrDefault();

        if (!IsAuthentic(options.Value.WebhookSecret, dataId, requestId, signature))
            return Unauthorized();

        if (!TryParse(rawBody, out var notification)
            || !string.Equals(notification.Type, "payment", StringComparison.OrdinalIgnoreCase))
            return Ok();

        // A query string manda; o corpo serve de fallback.
        var mpPaymentId = long.TryParse(dataId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromQuery)
            ? fromQuery
            : notification.DataId;

        if (notification.UserId is not { } mpUserId || mpPaymentId is not { } paymentId)
            return Ok();

        var account = await db.ClinicMercadoPagoAccounts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.MpUserId == mpUserId, ct);
        if (account is null)
        {
            logger.LogInformation("Webhook do Mercado Pago com user_id desconhecido: {UserId}.", mpUserId);
            return Ok();
        }

        var eventId = $"{notification.Id}:{notification.Action}";
        if (await db.ProcessedWebhookEvents.AnyAsync(
                item => item.Provider == Provider && item.EventId == eventId, ct))
            return Ok();

        db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
        {
            Provider = Provider,
            EventId = eventId,
        });

        try
        {
            var payment = await sync.ApplyAsync(account.ClinicaId, paymentId, ct);
            if (payment is null)
                logger.LogInformation("Webhook {EventId} não alterou nenhuma cobrança.", eventId);
        }
        catch
        {
            // Erro ao consultar/aplicar: não grava a idempotência para o Mercado Pago reenviar.
            db.ChangeTracker.Clear();
            throw;
        }

        await SaveProcessedAsync(ct);
        return Ok();
    }

    private async Task SaveProcessedAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            // Evento concorrente com o mesmo id: o índice único garante que só um aplicou.
        }
    }

    // Manifesto: id:{data.id};request-id:{x-request-id};ts:{ts}; — HMAC-SHA256 hex.
    private static bool IsAuthentic(string secret, string? dataId, string requestId, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(secret)
            || string.IsNullOrWhiteSpace(dataId)
            || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        string? ts = null;
        string? v1 = null;
        foreach (var part in signatureHeader.Split(','))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2)
                continue;

            var key = pair[0].Trim();
            var value = pair[1].Trim();
            if (key == "ts")
                ts = value;
            else if (key == "v1")
                v1 = value;
        }

        if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(v1))
            return false;

        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var expected = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest)));
        var actual = v1.ToUpperInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }

    private static bool TryParse(string rawBody, out Notification notification)
    {
        notification = new Notification(null, null, null, null, null);
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            notification = new Notification(
                Id: GetString(root, "id"),
                Type: GetString(root, "type"),
                Action: GetString(root, "action"),
                UserId: GetLong(GetProperty(root, "user_id")),
                DataId: root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                    ? GetLong(GetProperty(data, "id"))
                    : null);
            return notification.Id is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonElement? GetProperty(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) ? value : null;

    private static string? GetString(JsonElement parent, string name)
        => GetProperty(parent, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static long? GetLong(JsonElement? element)
    {
        if (element is not { } value)
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private sealed record Notification(string? Id, string? Type, string? Action, long? UserId, long? DataId);
}
