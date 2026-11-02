using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MultiClinica.API.Services.Billing;

public sealed record AbacateWebhookEvent(
    string Id, string Type, int? ClinicaId, string? Item,
    string? SubscriptionId, string? CustomerId, string? ProductId);

// Validação conforme https://docs.abacatepay.com/pages/webhooks/security.
public static class AbacateWebhook
{
    // Chave pública divulgada pela AbacatePay; pode ser sobrescrita por AbacatePay:WebhookHmacKey.
    private const string PublicHmacKey =
        "t9dXRhHHo3yDEj5pVDYz0frf7q6bMKyMRmxxCPIPp3RCplBfXRxqlC6ZpiWmOqj4L63qEaeUOtrCI8P0VMUgo6iIga2ri9ogaHFs0WIIywSMg0q7RmBfybe1E5XJcfC4IW3alNqym0tXoAKkzvfEjZxV6bE0oG2zJrNNYmUCKZyV0KZ3JS8Votf9EAWWYdiDkMkpbMdPggfh1EqHlVkMiTady6jOR3hyzGEHrIz2Ret0xHKMbiqkr9HS1JhNHDX9";

    private const string ClinicPrefix = "clinica:";

    /// Valida o segredo da query e o HMAC do corpo bruto (comparação em tempo constante).
    public static bool IsAuthentic(AbacatePayOptions options, string? secretFromQuery, string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(options.WebhookSecret) || !FixedTimeEquals(options.WebhookSecret, secretFromQuery))
            return false;
        if (string.IsNullOrEmpty(signatureHeader))
            return false;

        var key = string.IsNullOrWhiteSpace(options.WebhookHmacKey) ? PublicHmacKey : options.WebhookHmacKey;
        var expected = Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(rawBody)));
        return FixedTimeEquals(expected, signatureHeader);
    }

    /// Extrai os campos acima do JSON do evento; null se o JSON não for um evento reconhecível.
    public static AbacateWebhookEvent? Parse(string rawBody)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            if (!root.TryGetProperty("event", out var eventProperty) || eventProperty.ValueKind != JsonValueKind.String)
                return null;
            var type = eventProperty.GetString();
            if (string.IsNullOrWhiteSpace(type))
                return null;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;

            var checkout = GetObject(data, "checkout");
            var subscription = GetObject(data, "subscription");
            var payment = GetObject(data, "payment");
            var customer = GetObject(data, "customer");

            var externalId = GetString(checkout, "externalId")
                ?? GetString(payment, "externalId")
                ?? GetString(subscription, "externalId")
                ?? GetString(data, "externalId");
            var (clinicaId, item) = ParseClinicReference(externalId);

            return new AbacateWebhookEvent(
                Id: GetString(root, "id") ?? string.Empty,
                Type: type,
                ClinicaId: clinicaId,
                Item: item,
                SubscriptionId: GetString(subscription, "id"),
                CustomerId: GetString(customer, "id") ?? GetString(checkout, "customerId"),
                ProductId: GetString(subscription, "productId")
                    ?? GetString(data, "productId")
                    ?? FirstItemId(checkout));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (actual is null)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }

    private static (int? ClinicaId, string? Item) ParseClinicReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith(ClinicPrefix, StringComparison.Ordinal))
            return (null, null);

        var rest = reference[ClinicPrefix.Length..];
        var nonce = rest.IndexOf('#');
        if (nonce >= 0) rest = rest[..nonce];
        var separator = rest.IndexOf('|');
        var idPart = separator >= 0 ? rest[..separator] : rest;
        var item = separator >= 0 ? rest[(separator + 1)..] : null;

        if (!int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var clinicaId))
            return (null, null);

        return (clinicaId, string.IsNullOrWhiteSpace(item) ? null : item);
    }

    private static JsonElement? GetObject(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object)
            return null;
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
            return null;
        return value;
    }

    private static string? GetString(JsonElement? parent, string propertyName)
    {
        if (parent is null)
            return null;

        var element = parent.Value;
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? FirstItemId(JsonElement? checkout)
    {
        if (checkout is null)
            return null;

        var element = checkout.Value;
        if (!element.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in items.EnumerateArray())
            return GetString(item, "id");

        return null;
    }
}
