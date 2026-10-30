using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.MercadoPago;

// Cliente HTTP da Payments API e do OAuth do Mercado Pago.
// O access token é sempre o da clínica (por requisição), nunca um token global.
public sealed class MercadoPagoClient(HttpClient http, IOptions<MercadoPagoOptions> options) : IMercadoPagoClient
{
    private readonly MercadoPagoOptions _options = options.Value;

    public async Task<MpTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["code_verifier"] = codeVerifier,
        };
        if (_options.TestMode) form["test_token"] = "true";
        using var response = await http.PostAsync("oauth/token", new FormUrlEncodedContent(form), ct);

        return await ReadTokenAsync(response, ct);
    }

    public async Task<MpTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        using var response = await http.PostAsync("oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }), ct);

        return await ReadTokenAsync(response, ct);
    }

    public async Task<MpPayment> CreatePaymentAsync(
        string accessToken, MpPaymentRequest request, string idempotencyKey, CancellationToken ct)
    {
        var payer = new Dictionary<string, object?>
        {
            ["email"] = request.PayerEmail,
        };
        if (!string.IsNullOrWhiteSpace(request.PayerIdentificationType)
            && !string.IsNullOrWhiteSpace(request.PayerIdentificationNumber))
        {
            payer["identification"] = new Dictionary<string, string>
            {
                ["type"] = request.PayerIdentificationType,
                ["number"] = request.PayerIdentificationNumber,
            };
        }

        var body = new Dictionary<string, object?>
        {
            ["transaction_amount"] = request.TransactionAmount,
            ["description"] = request.Description,
            ["external_reference"] = request.ExternalReference,
            ["notification_url"] = request.NotificationUrl,
            ["payment_method_id"] = request.PaymentMethodId,
            ["payer"] = payer,
        };

        if (!string.IsNullOrWhiteSpace(request.CardToken))
        {
            body["token"] = request.CardToken;
            body["three_d_secure_mode"] = "optional";
        }
        if (request.Installments is { } installments)
            body["installments"] = installments;
        if (!string.IsNullOrWhiteSpace(request.IssuerId))
            body["issuer_id"] = request.IssuerId;
        if (request.DateOfExpiration is { } expiration)
            body["date_of_expiration"] = Iso8601(expiration);

        using var message = Request(HttpMethod.Post, "v1/payments", accessToken);
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = Json(body);

        using var response = await http.SendAsync(message, ct);
        return await ReadPaymentAsync(response, ct);
    }

    public async Task<MpPayment> GetPaymentAsync(string accessToken, long paymentId, CancellationToken ct)
    {
        using var message = Request(HttpMethod.Get, $"v1/payments/{paymentId}", accessToken);
        using var response = await http.SendAsync(message, ct);
        return await ReadPaymentAsync(response, ct);
    }

    public async Task RefundAsync(string accessToken, long paymentId, string idempotencyKey, CancellationToken ct)
    {
        using var message = Request(HttpMethod.Post, $"v1/payments/{paymentId}/refunds", accessToken);
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = Json(new Dictionary<string, object?>());

        using var response = await http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string accessToken)
    {
        var message = new HttpRequestMessage(method, path);
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return message;
    }

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static string Iso8601(DateTime value)
    {
        var offset = value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value),
            DateTimeKind.Local => new DateTimeOffset(value),
            _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
        };
        return offset.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
    }

    private static async Task<MpTokenResponse> ReadTokenAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        using var document = await ReadJsonAsync(response, ct);
        var root = document.RootElement;

        return new MpTokenResponse(
            String(root, "access_token"),
            String(root, "refresh_token"),
            root.GetProperty("expires_in").GetInt32(),
            root.GetProperty("user_id").GetInt64(),
            String(root, "public_key"),
            root.TryGetProperty("live_mode", out var liveMode) && liveMode.GetBoolean());
    }

    private static async Task<MpPayment> ReadPaymentAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        using var document = await ReadJsonAsync(response, ct);
        var root = document.RootElement;

        return new MpPayment(
            root.GetProperty("id").GetInt64(),
            String(root, "status"),
            StringOrNull(root, "status_detail"),
            StringOrNull(root, "external_reference"),
            root.TryGetProperty("collector_id", out var collector) && collector.ValueKind == JsonValueKind.Number
                ? collector.GetInt64()
                : null,
            StringOrNull(root, "payment_type_id"),
            StringOrNull(root, "payment_method_id"),
            DateTimeOrNull(root, "date_approved"));
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new MercadoPagoApiException((int)response.StatusCode, body);
    }

    private static string String(JsonElement root, string name) => root.GetProperty(name).GetString() ?? string.Empty;

    private static string? StringOrNull(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTime? DateTimeOrNull(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        return DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }
}
