using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

// Contrato da forma de pagamento Manual/Online no create e no update.
public class PaymentMethodTests
{
    private const string Online = "Online (Mercado Pago)";
    private const string Pix = "Pix";
    private const string Boleto = "Boleto";

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_rejects_unknown_payment_method_and_creates_nothing()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsJsonAsync("/api/payments", CreateBody(seed, "Cartão"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_PAYMENT_METHOD", (await ReadJsonAsync(response)).GetProperty("code").GetString());
        Assert.Equal(1, await CountPaymentsAsync(app));
        Assert.Equal(0, await CountOutboxAsync(app));
    }

    [Fact]
    public async Task Create_online_without_mercado_pago_returns_400_and_creates_nothing()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { ConnectMp = false });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsJsonAsync("/api/payments", CreateBody(seed, Online));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MERCADOPAGO_NOT_CONNECTED", (await ReadJsonAsync(response)).GetProperty("code").GetString());
        Assert.Equal(1, await CountPaymentsAsync(app));
    }

    [Fact]
    public async Task Create_online_without_the_plan_feature_returns_400()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { Plan = ClinicPlan.Free });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsJsonAsync("/api/payments", CreateBody(seed, Online));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ONLINE_PAYMENTS_NOT_AVAILABLE", (await ReadJsonAsync(response)).GetProperty("code").GetString());
        Assert.Equal(1, await CountPaymentsAsync(app));
    }

    [Fact]
    public async Task Create_online_paid_returns_400()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsJsonAsync("/api/payments",
            CreateBody(seed, Online, status: "Paid"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ONLINE_PAYMENT_MUST_BE_PENDING", (await ReadJsonAsync(response)).GetProperty("code").GetString());
        Assert.Equal(1, await CountPaymentsAsync(app));
    }

    [Fact]
    public async Task Create_online_with_send_and_no_patient_email_returns_400()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { PatientEmail = null });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsJsonAsync("/api/payments",
            CreateBody(seed, Online, sendOnline: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PATIENT_EMAIL_REQUIRED", (await ReadJsonAsync(response)).GetProperty("code").GetString());
        Assert.Equal(1, await CountPaymentsAsync(app));
        Assert.Equal(0, await CountOutboxAsync(app));
    }

    [Fact]
    public async Task Create_online_with_send_generates_link_and_queues_the_charge_email()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { PatientEmail = "ana@example.com" });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsJsonAsync("/api/payments",
            CreateBody(seed, Online, sendOnline: true));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var paymentId = body.GetProperty("id").GetInt32();
        Assert.True(body.GetProperty("hasPaymentLink").GetBoolean());

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == paymentId);
            Assert.False(string.IsNullOrWhiteSpace(payment.PaymentLinkToken));

            var job = await db.PatientEmailOutbox.SingleAsync();
            Assert.Equal(PatientEmailOutboxType.PaymentCharge, job.Type);
            Assert.Equal(paymentId, job.PaymentId);
            Assert.Null(job.PatientAccountId);
            Assert.StartsWith($"payment-charge:{paymentId}:", job.DeduplicationKey);
        });
    }

    [Fact]
    public async Task Create_online_without_send_creates_no_link_or_job()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { PatientEmail = "ana@example.com" });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsJsonAsync("/api/payments", CreateBody(seed, Online));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var paymentId = (await ReadJsonAsync(response)).GetProperty("id").GetInt32();

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == paymentId);
            Assert.Null(payment.PaymentLinkToken);
            Assert.Empty(await db.PatientEmailOutbox.ToListAsync());
        });
    }

    [Fact]
    public async Task Create_manual_with_send_ignores_the_send_flag()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { PatientEmail = "ana@example.com" });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsJsonAsync("/api/payments",
            CreateBody(seed, Pix, sendOnline: true));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var paymentId = (await ReadJsonAsync(response)).GetProperty("id").GetInt32();

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == paymentId);
            Assert.Equal(Pix, payment.PaymentMethod);
            Assert.Null(payment.PaymentLinkToken);
            Assert.Empty(await db.PatientEmailOutbox.ToListAsync());
        });
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_keeps_a_legacy_payment_method()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { PaymentMethod = Boleto });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PutAsJsonAsync($"/api/payments/{seed.PaymentId}",
            UpdateBody(seed, Boleto));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(Boleto, payment.PaymentMethod);
        });
    }

    [Fact]
    public async Task Update_cannot_change_a_charge_to_online()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PutAsJsonAsync($"/api/payments/{seed.PaymentId}",
            UpdateBody(seed, Online));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_PAYMENT_METHOD", (await ReadJsonAsync(response)).GetProperty("code").GetString());
        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(Pix, payment.PaymentMethod);
        });
    }

    [Fact]
    public async Task Update_cannot_change_from_online_to_manual_with_a_pending_online_payment()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { PaymentMethod = Online, MpStatus = "pending" });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PutAsJsonAsync($"/api/payments/{seed.PaymentId}",
            UpdateBody(seed, Pix));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PAYMENT_HAS_PENDING_ONLINE_PAYMENT",
            (await ReadJsonAsync(response)).GetProperty("code").GetString());
        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(Online, payment.PaymentMethod);
        });
    }

    [Fact]
    public async Task Update_can_change_from_online_to_manual_without_a_pending_online_payment()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { PaymentMethod = Online });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PutAsJsonAsync($"/api/payments/{seed.PaymentId}",
            UpdateBody(seed, Pix));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(Pix, payment.PaymentMethod);
        });
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private static Dictionary<string, object?> CreateBody(
        MercadoPagoTestSupport.SeededClinic seed,
        string paymentMethod,
        string status = "Pending",
        bool? sendOnline = null,
        string referenceMonth = "2026-10-01")
    {
        var body = new Dictionary<string, object?>
        {
            ["responsavelId"] = seed.AdminId,
            ["patientId"] = seed.PatientId,
            ["planId"] = seed.PlanId,
            ["referenceMonth"] = referenceMonth,
            ["paymentMethod"] = paymentMethod,
            ["status"] = status,
            ["paidAt"] = (string?)null,
            ["paymentDate"] = (string?)null,
        };
        if (sendOnline.HasValue)
            body["sendOnline"] = sendOnline.Value;
        return body;
    }

    private static Dictionary<string, object?> UpdateBody(
        MercadoPagoTestSupport.SeededClinic seed, string paymentMethod)
        => new()
        {
            ["planId"] = seed.PlanId,
            ["referenceMonth"] = "2026-09-01",
            ["paymentMethod"] = paymentMethod,
            ["status"] = "Pending",
            ["paidAt"] = (string?)null,
            ["paymentDate"] = (string?)null,
        };

    private static async Task<int> CountPaymentsAsync(MultiClinicaFactory app)
    {
        var count = 0;
        await app.SeedAsync(async db => count = await db.Payments.CountAsync());
        return count;
    }

    private static async Task<int> CountOutboxAsync(MultiClinicaFactory app)
    {
        var count = 0;
        await app.SeedAsync(async db => count = await db.PatientEmailOutbox.CountAsync());
        return count;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
