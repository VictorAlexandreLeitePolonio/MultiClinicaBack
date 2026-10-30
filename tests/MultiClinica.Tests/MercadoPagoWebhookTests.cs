using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Models;
using MultiClinica.API.Services.MercadoPago;
using Xunit;

namespace MultiClinica.Tests;

public class MercadoPagoWebhookTests
{
    [Fact]
    public async Task Webhook_rejects_invalid_or_missing_signature()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());
        fake.Payments[900] = Approved(900, seed.PaymentId);
        using var client = app.CreateClient();

        var body = PaymentBody(900);
        var signed = Sign(body, "900");

        var wrongSignature = await PostAsync(client, signed with { Signature = "ts=1704908010,v1=deadbeef" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongSignature.StatusCode);

        var missingQueryId = await PostAsync(client, signed, includeDataId: false);
        Assert.Equal(HttpStatusCode.Unauthorized, missingQueryId.StatusCode);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.Equal(0, await db.ProcessedWebhookEvents.CountAsync());
        });
    }

    [Fact]
    public async Task Webhook_applies_payment_once_and_ignores_unknown_user_and_other_events()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());
        fake.Payments[900] = Approved(900, seed.PaymentId);
        using var client = app.CreateClient();

        var signed = Sign(PaymentBody(900), "900");
        var first = await PostAsync(client, signed);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.True(payment.PaidOnline);
            Assert.Equal(900, payment.MpPaymentId);
            Assert.Equal(1, await db.ProcessedWebhookEvents.CountAsync());
        });

        // Mesma notificação de novo: não consulta nem aplica outra vez.
        fake.Payments[900] = Approved(900, seed.PaymentId) with { Status = "refunded" };
        var duplicate = await PostAsync(client, signed);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.Null(payment.OnlineAlert);
            Assert.Equal(1, await db.ProcessedWebhookEvents.CountAsync());
        });

        // user_id que não pertence a nenhuma clínica: 200 sem efeitos.
        var unknownUser = Sign(PaymentBody(901, userId: 999), "901");
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, unknownUser)).StatusCode);

        // Tipo diferente de payment: 200 sem efeitos.
        var otherType = Sign(PaymentBody(900, type: "merchant_order", id: 12346), "900");
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, otherType)).StatusCode);

        await app.SeedAsync(async db => Assert.Equal(1, await db.ProcessedWebhookEvents.CountAsync()));
    }

    [Fact]
    public async Task Webhook_refunded_after_a_system_refund_keeps_the_charge_cancelled()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            Status = PaymentStatus.Cancelled,
            PaidOnline = true,
            MpPaymentId = 700,
            RefundedAt = DateTime.UtcNow,
        });
        fake.Payments[700] = Approved(700, seed.PaymentId) with { Status = "refunded" };
        using var client = app.CreateClient();

        var response = await PostAsync(client, Sign(PaymentBody(700), "700"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Cancelled, payment.Status);
            Assert.NotNull(payment.RefundedAt);
            Assert.Null(payment.OnlineAlert);
        });
    }

    [Fact]
    public async Task Webhook_marks_charge_paid_from_the_query_data_id_even_without_body_data()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());
        fake.Payments[902] = Approved(902, seed.PaymentId);
        using var client = app.CreateClient();

        var body = $$"""
            {
              "id": 12347,
              "live_mode": false,
              "type": "payment",
              "action": "payment.updated",
              "user_id": {{MercadoPagoTestSupport.DefaultMpUserId}}
            }
            """;
        var response = await PostAsync(client, Sign(body, "902"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.Equal(902, payment.MpPaymentId);
        });
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private static MpPayment Approved(long id, int paymentId)
        => new(id, "approved", "accredited", $"payment:{paymentId}",
            MercadoPagoTestSupport.DefaultMpUserId, "bank_transfer", "pix", DateTime.UtcNow);

    private static string PaymentBody(
        long dataId,
        long userId = MercadoPagoTestSupport.DefaultMpUserId,
        string type = "payment",
        long id = 12345,
        string action = "payment.updated")
        => $$"""
            {
              "id": {{id}},
              "live_mode": false,
              "type": "{{type}}",
              "date_created": "2026-10-30T12:00:00Z",
              "action": "{{action}}",
              "user_id": {{userId}},
              "data": { "id": "{{dataId}}" }
            }
            """;

    private sealed record SignedRequest(string Body, string DataId, string RequestId, string Signature);

    private static SignedRequest Sign(string body, string dataId, string ts = "1704908010")
    {
        var requestId = Guid.NewGuid().ToString();
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var hash = Convert.ToHexString(
                HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(MercadoPagoTestSupport.WebhookSecret),
                    Encoding.UTF8.GetBytes(manifest)))
            .ToLowerInvariant();
        return new SignedRequest(body, dataId, requestId, $"ts={ts},v1={hash}");
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, SignedRequest signed, bool includeDataId = true)
    {
        var url = "/api/payments/webhooks/mercadopago"
            + (includeDataId ? $"?data.id={signed.DataId}&type=payment" : string.Empty);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(signed.Body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-signature", signed.Signature);
        request.Headers.Add("x-request-id", signed.RequestId);
        return client.SendAsync(request);
    }
}
