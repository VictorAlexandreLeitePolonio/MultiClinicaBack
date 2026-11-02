using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiClinica.API.Services.MercadoPago;
using Xunit;

namespace MultiClinica.Tests;

public class MercadoPagoClientTests
{
    private sealed record Call(HttpMethod Method, string Path, string? Authorization, string? IdempotencyKey, string Body);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add(new Call(
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Idempotency-Key", out var keys) ? keys.Single() : null,
                body));
            return respond(request);
        }
    }

    private const string TokenJson = """
        {
          "access_token": "APP_USR-1",
          "refresh_token": "TG-1",
          "expires_in": 15552000,
          "user_id": 123456,
          "public_key": "PUB-1",
          "live_mode": true
        }
        """;

    private const string PaymentJson = """
        {
          "id": 987654,
          "status": "approved",
          "status_detail": "accredited",
          "external_reference": "payment:7",
          "collector_id": 123456,
          "payment_type_id": "credit_card",
          "payment_method_id": "visa",
          "date_approved": "2026-10-30T15:04:05.000-03:00"
        }
        """;

    private const string MeJson = """
        {
          "id": 123456,
          "nickname": "CLINICA_TESTE",
          "email": "clinica@test.com"
        }
        """;

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (MercadoPagoClient Client, FakeHandler Handler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> respond, bool testMode = false)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        var options = Options.Create(new MercadoPagoOptions
        {
            ClientId = "client-1",
            ClientSecret = "secret-1",
            RedirectUri = "https://api.test/api/mercadopago/callback",
            ApiBaseUrl = "https://api.test/",
            TestMode = testMode,
        });
        return (new MercadoPagoClient(http, options), handler);
    }

    private static Dictionary<string, string> ParseForm(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty);

    [Fact]
    public async Task ExchangeCodeAsync_sends_pkce_form_and_maps_token()
    {
        var (client, handler) = Build(_ => Json(TokenJson));

        var token = await client.ExchangeCodeAsync("code-1", "verifier-1", CancellationToken.None);

        Assert.Equal("APP_USR-1", token.AccessToken);
        Assert.Equal("TG-1", token.RefreshToken);
        Assert.Equal(15552000, token.ExpiresIn);
        Assert.Equal(123456, token.UserId);
        Assert.Equal("PUB-1", token.PublicKey);
        Assert.True(token.LiveMode);

        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("/oauth/token", call.Path);
        var form = ParseForm(call.Body);
        Assert.Equal("client-1", form["client_id"]);
        Assert.Equal("secret-1", form["client_secret"]);
        Assert.Equal("authorization_code", form["grant_type"]);
        Assert.Equal("code-1", form["code"]);
        Assert.Equal("verifier-1", form["code_verifier"]);
        Assert.Equal("https://api.test/api/mercadopago/callback", form["redirect_uri"]);
        Assert.False(form.ContainsKey("test_token"));
    }

    [Fact]
    public async Task ExchangeCodeAsync_asks_for_sandbox_token_in_test_mode()
    {
        var (client, handler) = Build(_ => Json(TokenJson), testMode: true);

        await client.ExchangeCodeAsync("code-1", "verifier-1", CancellationToken.None);

        Assert.Equal("true", ParseForm(Assert.Single(handler.Calls).Body)["test_token"]);
    }

    [Fact]
    public async Task RefreshAsync_sends_refresh_token_and_maps_the_new_one()
    {
        var (client, handler) = Build(_ => Json(TokenJson));

        var token = await client.RefreshAsync("TG-OLD", CancellationToken.None);

        Assert.Equal("TG-1", token.RefreshToken);
        var form = ParseForm(Assert.Single(handler.Calls).Body);
        Assert.Equal("refresh_token", form["grant_type"]);
        Assert.Equal("TG-OLD", form["refresh_token"]);
    }

    [Fact]
    public async Task CreatePaymentAsync_with_card_sends_expected_json_and_headers()
    {
        var (client, handler) = Build(_ => Json(PaymentJson));

        var payment = await client.CreatePaymentAsync("APP_USR-1", new MpPaymentRequest(
            TransactionAmount: 120.50m,
            Description: "Mensal — 2026-10",
            ExternalReference: "payment:7",
            NotificationUrl: "https://api.test/api/payments/webhooks/mercadopago",
            PaymentMethodId: "visa",
            CardToken: "card-token",
            Installments: 3,
            IssuerId: "issuer-1",
            PayerEmail: "paciente@test.com",
            PayerIdentificationType: "CPF",
            PayerIdentificationNumber: "12345678900",
            DateOfExpiration: new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc)),
            "idem-1", CancellationToken.None);

        Assert.Equal(987654, payment.Id);
        Assert.Equal("approved", payment.Status);
        Assert.Equal("accredited", payment.StatusDetail);
        Assert.Equal("payment:7", payment.ExternalReference);
        Assert.Equal(123456, payment.CollectorId);
        Assert.Equal("credit_card", payment.PaymentTypeId);
        Assert.Equal("visa", payment.PaymentMethodId);
        Assert.Equal(new DateTime(2026, 10, 30, 18, 4, 5, DateTimeKind.Utc), payment.DateApproved);

        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("/v1/payments", call.Path);
        Assert.Equal("Bearer APP_USR-1", call.Authorization);
        Assert.Equal("idem-1", call.IdempotencyKey);

        var body = JsonDocument.Parse(call.Body).RootElement;
        Assert.Equal(120.50m, body.GetProperty("transaction_amount").GetDecimal());
        Assert.Equal("Mensal — 2026-10", body.GetProperty("description").GetString());
        Assert.Equal("payment:7", body.GetProperty("external_reference").GetString());
        Assert.Equal("https://api.test/api/payments/webhooks/mercadopago",
            body.GetProperty("notification_url").GetString());
        Assert.Equal("visa", body.GetProperty("payment_method_id").GetString());
        Assert.Equal("card-token", body.GetProperty("token").GetString());
        Assert.Equal(3, body.GetProperty("installments").GetInt32());
        Assert.Equal("issuer-1", body.GetProperty("issuer_id").GetString());
        Assert.Equal("optional", body.GetProperty("three_d_secure_mode").GetString());
        Assert.EndsWith("+00:00", body.GetProperty("date_of_expiration").GetString());
        Assert.Equal("paciente@test.com", body.GetProperty("payer").GetProperty("email").GetString());
        Assert.Equal("CPF", body.GetProperty("payer").GetProperty("identification").GetProperty("type").GetString());
        Assert.Equal("12345678900", body.GetProperty("payer").GetProperty("identification").GetProperty("number").GetString());
        Assert.False(body.TryGetProperty("application_fee", out _));
    }

    [Fact]
    public async Task CreatePaymentAsync_with_pix_omits_card_fields_and_sets_expiration()
    {
        var (client, handler) = Build(_ => Json("""
            {"id": 1, "status": "pending", "status_detail": "pending_waiting_transfer",
             "external_reference": "payment:7", "collector_id": 123456,
             "payment_type_id": "bank_transfer", "payment_method_id": "pix", "date_approved": null}
            """));

        await client.CreatePaymentAsync("tok", new MpPaymentRequest(
            10m, "Plano — 2026-10", "payment:7", "https://api.test/webhook", "pix",
            CardToken: null, Installments: null, IssuerId: null,
            PayerEmail: "p@test.com", PayerIdentificationType: null, PayerIdentificationNumber: null,
            DateOfExpiration: new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc)),
            "idem-pix", CancellationToken.None);

        var body = JsonDocument.Parse(Assert.Single(handler.Calls).Body).RootElement;
        Assert.Equal("pix", body.GetProperty("payment_method_id").GetString());
        Assert.False(body.TryGetProperty("token", out _));
        Assert.False(body.TryGetProperty("installments", out _));
        Assert.False(body.TryGetProperty("issuer_id", out _));
        Assert.False(body.TryGetProperty("three_d_secure_mode", out _));
        Assert.False(body.GetProperty("payer").TryGetProperty("identification", out _));
        Assert.True(body.TryGetProperty("date_of_expiration", out _));
    }

    [Fact]
    public async Task GetPaymentAsync_uses_bearer_and_returns_mapped_payment()
    {
        var (client, handler) = Build(_ => Json(PaymentJson));

        var payment = await client.GetPaymentAsync("APP_USR-1", 987654, CancellationToken.None);

        Assert.Equal(987654, payment.Id);
        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Get, call.Method);
        Assert.Equal("/v1/payments/987654", call.Path);
        Assert.Equal("Bearer APP_USR-1", call.Authorization);
    }

    [Fact]
    public async Task GetMeAsync_uses_bearer_and_maps_id_nickname_and_email()
    {
        var (client, handler) = Build(_ => Json(MeJson));

        var me = await client.GetMeAsync("APP_USR-1", CancellationToken.None);

        Assert.Equal(123456, me.Id);
        Assert.Equal("CLINICA_TESTE", me.Nickname);
        Assert.Equal("clinica@test.com", me.Email);

        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Get, call.Method);
        Assert.Equal("/users/me", call.Path);
        Assert.Equal("Bearer APP_USR-1", call.Authorization);
    }

    [Fact]
    public async Task GetMeAsync_missing_optional_fields_become_null()
    {
        var (client, _) = Build(_ => Json("""{"id": 1}"""));

        var me = await client.GetMeAsync("APP_USR-1", CancellationToken.None);

        Assert.Equal(1, me.Id);
        Assert.Null(me.Nickname);
        Assert.Null(me.Email);
    }

    [Fact]
    public async Task GetMeAsync_non_success_becomes_MercadoPagoApiException_with_status()
    {
        var (client, _) = Build(_ => Json("""{"message":"invalid_token"}""", HttpStatusCode.Unauthorized));

        var exception = await Assert.ThrowsAsync<MercadoPagoApiException>(
            () => client.GetMeAsync("APP_USR-1", CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
        Assert.Contains("invalid_token", exception.Body);
    }

    [Fact]
    public async Task RefundAsync_posts_empty_body_with_idempotency()
    {
        var (client, handler) = Build(_ => Json("{}", HttpStatusCode.Created));

        await client.RefundAsync("APP_USR-1", 987654, "refund:7", CancellationToken.None);

        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("/v1/payments/987654/refunds", call.Path);
        Assert.Equal("Bearer APP_USR-1", call.Authorization);
        Assert.Equal("refund:7", call.IdempotencyKey);
        Assert.Equal("{}", call.Body);
    }

    [Fact]
    public async Task Non_success_becomes_MercadoPagoApiException_with_status_and_body()
    {
        var (client, _) = Build(_ => Json("""{"message":"invalid_token"}""", HttpStatusCode.Unauthorized));

        var exception = await Assert.ThrowsAsync<MercadoPagoApiException>(
            () => client.RefreshAsync("TG-OLD", CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
        Assert.Contains("invalid_token", exception.Body);
    }
}
