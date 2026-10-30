using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using MultiClinica.API.Services.Interfaces;
using MultiClinica.API.Services.MercadoPago;
using Xunit;

namespace MultiClinica.Tests;

public class OnlinePaymentTests
{
    // ── Envio e link ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_blocks_payment_not_pending()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { Status = PaymentStatus.Cancelled });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsync($"/api/payments/{seed.PaymentId}/send", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PAYMENT_NOT_PENDING", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Send_blocks_without_connected_mercado_pago()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { ConnectMp = false });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsync($"/api/payments/{seed.PaymentId}/send", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MERCADOPAGO_NOT_CONNECTED", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Send_blocks_patient_without_email()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { PatientEmail = null });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsync($"/api/payments/{seed.PaymentId}/send", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PATIENT_EMAIL_REQUIRED", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Send_creates_link_and_queues_payment_charge_without_patient_account()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { PatientEmail = "ana@example.com" });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsync($"/api/payments/{seed.PaymentId}/send", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await ReadJsonAsync(response)).GetProperty("queued").GetBoolean());

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.False(string.IsNullOrWhiteSpace(payment.PaymentLinkToken));

            var job = await db.PatientEmailOutbox.SingleAsync();
            Assert.Equal(PatientEmailOutboxType.PaymentCharge, job.Type);
            Assert.Equal(seed.ClinicaId, job.ClinicaId);
            Assert.Equal(seed.PatientId, job.PatientId);
            Assert.Equal(seed.PaymentId, job.PaymentId);
            Assert.Null(job.PatientAccountId);
            Assert.StartsWith($"payment-charge:{seed.PaymentId}:", job.DeduplicationKey);
        });
    }

    [Fact]
    public async Task Link_returns_url_and_reuses_the_same_token()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var first = await ReadJsonAsync(await client.GetAsync($"/api/payments/{seed.PaymentId}/link"));
        var second = await ReadJsonAsync(await client.GetAsync($"/api/payments/{seed.PaymentId}/link"));

        var url = first.GetProperty("url").GetString();
        Assert.NotNull(url);
        Assert.Contains("/pagar/", url);
        Assert.Equal(url, second.GetProperty("url").GetString());

        var payment = await ReadJsonAsync(await client.GetAsync($"/api/payments/{seed.PaymentId}"));
        Assert.True(payment.GetProperty("hasPaymentLink").GetBoolean());
    }

    [Fact]
    public async Task Link_blocks_when_payment_is_paid()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { Status = PaymentStatus.Paid, PaidOnline = true, MpPaymentId = 1 });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.GetAsync($"/api/payments/{seed.PaymentId}/link");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PAYMENT_NOT_PENDING", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    // ── Página pública ───────────────────────────────────────────────────────

    [Fact]
    public async Task Public_get_returns_only_safe_patient_data()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            PaymentLinkToken = "tok-public",
            PatientName = "Ana Maria Souza",
            Amount = 199.90m,
        });
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/public/payments/tok-public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Clínica MP", body.GetProperty("clinicName").GetString());
        Assert.Equal("Ana", body.GetProperty("patientFirstName").GetString());
        Assert.Equal(199.90m, body.GetProperty("amount").GetDecimal());
        Assert.Equal("Mensal", body.GetProperty("planName").GetString());
        Assert.Equal("2026-09-01", body.GetProperty("referenceMonth").GetString());
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(MercadoPagoTestSupport.PublicKey, body.GetProperty("publicKey").GetString());
        Assert.True(body.GetProperty("acceptPix").GetBoolean());
        Assert.True(body.GetProperty("acceptCredit").GetBoolean());
        Assert.True(body.GetProperty("acceptDebit").GetBoolean());
        Assert.Equal(1, body.GetProperty("maxInstallments").GetInt32());
        Assert.False(body.TryGetProperty("patientEmail", out _));
    }

    [Fact]
    public async Task Public_get_returns_not_found_for_unknown_token_and_unavailable_when_not_payable()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            PaymentLinkToken = "tok-cancelled",
            Status = PaymentStatus.Cancelled,
        });
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            AdminEmail = "sem-mp@test.local",
            ClinicName = "Clínica Sem MP",
            ConnectMp = false,
            PaymentLinkToken = "tok-sem-mp",
        });
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            AdminEmail = "pago-manual@test.local",
            ClinicName = "Clínica Pago Manual",
            PaymentLinkToken = "tok-paid-manual",
            Status = PaymentStatus.Paid,
        });
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            AdminEmail = "pago-online@test.local",
            ClinicName = "Clínica Pago Online",
            PaymentLinkToken = "tok-paid-online",
            Status = PaymentStatus.Paid,
            PaidOnline = true,
            MpPaymentId = 77,
        });
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/public/payments/nao-existe")).StatusCode);

        var cancelled = await ReadJsonAsync(await client.GetAsync("/api/public/payments/tok-cancelled"));
        Assert.Equal("Unavailable", cancelled.GetProperty("status").GetString());

        var disconnected = await ReadJsonAsync(await client.GetAsync("/api/public/payments/tok-sem-mp"));
        Assert.Equal("Unavailable", disconnected.GetProperty("status").GetString());

        var manuallyPaid = await ReadJsonAsync(await client.GetAsync("/api/public/payments/tok-paid-manual"));
        Assert.Equal("Unavailable", manuallyPaid.GetProperty("status").GetString());

        var paidOnline = await ReadJsonAsync(await client.GetAsync("/api/public/payments/tok-paid-online"));
        Assert.Equal("Paid", paidOnline.GetProperty("status").GetString());
    }

    // ── /pay ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pay_uses_database_amount_and_marks_payment_paid()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            PaymentLinkToken = "tok-pay",
            PatientEmail = "ana@example.com",
            Amount = 199.90m,
        });

        fake.OnCreate = request => new MpPayment(
            9001, "approved", "accredited", request.ExternalReference,
            MercadoPagoTestSupport.DefaultMpUserId, "bank_transfer", "pix",
            new DateTime(2026, 10, 30, 2, 0, 0, DateTimeKind.Utc));

        using var client = app.CreateClient();
        using var admin = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");
        var response = await client.PostAsJsonAsync("/api/public/payments/tok-pay/pay",
            Payload("pix", payerEmail: "ana@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(9001, body.GetProperty("paymentId").GetInt64());
        Assert.Equal("approved", body.GetProperty("status").GetString());

        var created = Assert.Single(fake.Created);
        Assert.Equal("tok", created.AccessToken);
        Assert.Equal(199.90m, created.Request.TransactionAmount); // nunca o valor do cliente
        Assert.Equal($"payment:{seed.PaymentId}", created.Request.ExternalReference);
        Assert.Equal("https://api.test.local/api/payments/webhooks/mercadopago", created.Request.NotificationUrl);
        Assert.Equal("pix", created.Request.PaymentMethodId);
        Assert.Null(created.Request.CardToken);
        Assert.Null(created.Request.Installments);
        Assert.NotNull(created.Request.DateOfExpiration);
        Assert.InRange(created.Request.DateOfExpiration!.Value,
            DateTime.UtcNow.AddHours(23), DateTime.UtcNow.AddHours(25));
        Assert.Contains("Mensal", created.Request.Description);
        Assert.Contains("Setembro de 2026", created.Request.Description);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.True(payment.PaidOnline);
            Assert.Equal("Pix", payment.PaymentMethod);
            Assert.Equal(new DateOnly(2026, 10, 29), payment.PaidAt); // 02:00 UTC = 23:00 do dia anterior em SP
            Assert.Equal(9001, payment.MpPaymentId);
            Assert.Equal("approved", payment.MpStatus);
        });

        var detail = await ReadJsonAsync(await admin.GetAsync($"/api/payments/{seed.PaymentId}"));
        Assert.True(detail.GetProperty("paidOnline").GetBoolean());
        Assert.Equal("approved", detail.GetProperty("mpStatus").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("onlineAlert").ValueKind);
        Assert.True(detail.GetProperty("hasPaymentLink").GetBoolean());
    }

    [Fact]
    public async Task Online_payment_endpoints_require_the_plan_feature()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app,
            new ClinicSeedOptions { Plan = ClinicPlan.Free });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsync($"/api/payments/{seed.PaymentId}/send", null);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal("PLAN_REQUIRED", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Pay_rejects_payment_method_not_accepted_by_clinic()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            PaymentLinkToken = "tok-methods",
            AcceptPix = false,
            AcceptDebit = false,
            MaxInstallments = 2,
        });
        using var client = app.CreateClient();

        var pix = await client.PostAsJsonAsync("/api/public/payments/tok-methods/pay",
            Payload("pix", payerEmail: "ana@example.com"));
        Assert.Equal(HttpStatusCode.BadRequest, pix.StatusCode);
        Assert.Equal("PAYMENT_METHOD_NOT_ALLOWED", (await ReadJsonAsync(pix)).GetProperty("code").GetString());

        var debit = await client.PostAsJsonAsync("/api/public/payments/tok-methods/pay",
            Payload("debvisa", payerEmail: "ana@example.com", token: "card-token"));
        Assert.Equal(HttpStatusCode.BadRequest, debit.StatusCode);
        Assert.Equal("PAYMENT_METHOD_NOT_ALLOWED", (await ReadJsonAsync(debit)).GetProperty("code").GetString());

        Assert.Empty(fake.Created);
    }

    [Fact]
    public async Task Pay_rejects_installments_above_clinic_limit()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            PaymentLinkToken = "tok-installments",
            MaxInstallments = 2,
        });
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/public/payments/tok-installments/pay",
            Payload("visa", payerEmail: "ana@example.com", token: "card-token", installments: 3));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INSTALLMENTS_NOT_ALLOWED", (await ReadJsonAsync(response)).GetProperty("code").GetString());
        Assert.Empty(fake.Created);
    }

    [Fact]
    public async Task Pay_accepts_credit_with_installments_up_to_the_limit()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            PaymentLinkToken = "tok-card",
            MaxInstallments = 2,
        });
        using var client = app.CreateClient();

        fake.OnCreate = request => new MpPayment(
            9100, "in_process", "pending_challenge", request.ExternalReference,
            MercadoPagoTestSupport.DefaultMpUserId, "credit_card", "visa", null);

        var response = await client.PostAsJsonAsync("/api/public/payments/tok-card/pay",
            Payload("visa", payerEmail: "ana@example.com", token: "card-token", installments: 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = Assert.Single(fake.Created);
        Assert.Equal(2, created.Request.Installments);
        Assert.Equal("card-token", created.Request.CardToken);
        Assert.Null(created.Request.DateOfExpiration);
    }

    [Fact]
    public async Task Pay_rejects_payment_that_is_not_pending()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            PaymentLinkToken = "tok-paid",
            Status = PaymentStatus.Paid,
            PaidOnline = true,
            MpPaymentId = 10,
        });
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/public/payments/tok-paid/pay",
            Payload("pix", payerEmail: "ana@example.com"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PAYMENT_NOT_PENDING", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Pay_rejects_new_attempt_while_a_pix_is_in_progress()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            PaymentLinkToken = "tok-progress",
            MpPaymentId = 1234,
            MpStatus = "pending",
        });
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/public/payments/tok-progress/pay",
            Payload("pix", payerEmail: "ana@example.com"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PAYMENT_IN_PROGRESS", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    // ── Sync ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_pending_and_rejected_keep_the_charge_pending()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());

        fake.Payments[500] = Mp(500, seed.PaymentId, "pending", detail: "waiting_transfer");
        await ApplyAsync(app, seed.ClinicaId, 500);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.Equal(500, payment.MpPaymentId);
            Assert.Equal("pending", payment.MpStatus);
            Assert.Equal("waiting_transfer", payment.MpStatusDetail);
        });

        fake.Payments[500] = Mp(500, seed.PaymentId, "rejected", detail: "cc_rejected_other_reason");
        await ApplyAsync(app, seed.ClinicaId, 500);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.Equal("rejected", payment.MpStatus);
            Assert.Equal("cc_rejected_other_reason", payment.MpStatusDetail);
        });
    }

    [Fact]
    public async Task Sync_approved_marks_paid_with_method_and_local_date()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());

        fake.Payments[501] = Mp(501, seed.PaymentId, "approved", type: "credit_card",
            dateApproved: new DateTime(2026, 10, 30, 2, 0, 0, DateTimeKind.Utc));
        await ApplyAsync(app, seed.ClinicaId, 501);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.True(payment.PaidOnline);
            Assert.Equal(new DateOnly(2026, 10, 29), payment.PaidAt);
            Assert.Equal("Cartão de crédito", payment.PaymentMethod);
            Assert.Equal(501, payment.MpPaymentId);
            Assert.Equal("approved", payment.MpStatus);
        });
    }

    [Fact]
    public async Task Sync_refund_and_chargeback_alert_without_a_system_refund()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());

        fake.Payments[502] = Mp(502, seed.PaymentId, "approved");
        await ApplyAsync(app, seed.ClinicaId, 502);

        fake.Payments[502] = Mp(502, seed.PaymentId, "refunded");
        await ApplyAsync(app, seed.ClinicaId, 502);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.False(payment.PaidOnline);
            Assert.Null(payment.PaidAt);
            Assert.Equal("Estornado", payment.OnlineAlert);
        });

        fake.Payments[502] = Mp(502, seed.PaymentId, "approved");
        await ApplyAsync(app, seed.ClinicaId, 502);

        fake.Payments[502] = Mp(502, seed.PaymentId, "charged_back");
        await ApplyAsync(app, seed.ClinicaId, 502);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.Equal("Contestado", payment.OnlineAlert);
        });
    }

    [Fact]
    public async Task Sync_ignores_wrong_external_reference_and_collector()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions());

        fake.Payments[503] = Mp(503, seed.PaymentId, "approved", externalReference: "payment:999999");
        Assert.Null(await ApplyAsync(app, seed.ClinicaId, 503));

        fake.Payments[504] = Mp(504, seed.PaymentId, "approved", collectorId: 999);
        Assert.Null(await ApplyAsync(app, seed.ClinicaId, 504));

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.False(payment.PaidOnline);
            Assert.Null(payment.MpPaymentId);
        });
    }

    [Fact]
    public async Task Sync_approved_duplicate_keeps_the_first_payment_and_sets_the_alert()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            Status = PaymentStatus.Paid,
            PaidOnline = true,
            PaidAt = new DateOnly(2026, 9, 10),
            MpPaymentId = 600,
        });

        fake.Payments[601] = Mp(601, seed.PaymentId, "approved");
        await ApplyAsync(app, seed.ClinicaId, 601);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.Equal(600, payment.MpPaymentId);
            Assert.Equal("PagamentoDuplicado:601", payment.OnlineAlert);
        });
    }

    // ── Reembolso ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refund_cancels_the_charge_and_a_later_refunded_webhook_does_not_reopen_it()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            Status = PaymentStatus.Paid,
            PaidOnline = true,
            PaidAt = new DateOnly(2026, 9, 10),
            MpPaymentId = 700,
            MpStatus = "approved",
        });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsync($"/api/payments/{seed.PaymentId}/refund", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refund = Assert.Single(fake.Refunds);
        Assert.Equal("tok", refund.AccessToken);
        Assert.Equal(700, refund.PaymentId);
        Assert.Equal($"refund:{seed.PaymentId}", refund.IdempotencyKey);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Cancelled, payment.Status);
            Assert.NotNull(payment.RefundedAt);
            Assert.Null(payment.OnlineAlert);
        });

        fake.Payments[700] = Mp(700, seed.PaymentId, "refunded");
        await ApplyAsync(app, seed.ClinicaId, 700);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Cancelled, payment.Status);
            Assert.NotNull(payment.RefundedAt);
            Assert.Null(payment.OnlineAlert);
        });
    }

    [Fact]
    public async Task Refund_returns_400_with_mercado_pago_error()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            Status = PaymentStatus.Paid,
            PaidOnline = true,
            MpPaymentId = 701,
        });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");
        fake.FailWithStatus = 400;

        var response = await client.PostAsync($"/api/payments/{seed.PaymentId}/refund", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MERCADOPAGO_REFUND_FAILED", (await ReadJsonAsync(response)).GetProperty("code").GetString());

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
        });
    }

    [Fact]
    public async Task Refund_duplicate_refunds_the_alerted_payment_and_clears_the_alert()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            Status = PaymentStatus.Paid,
            PaidOnline = true,
            MpPaymentId = 702,
            OnlineAlert = "PagamentoDuplicado:703",
        });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsync($"/api/payments/{seed.PaymentId}/refund-duplicate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refund = Assert.Single(fake.Refunds);
        Assert.Equal(703, refund.PaymentId);

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.Null(payment.OnlineAlert);
        });
    }

    [Fact]
    public async Task Refund_duplicate_requires_an_alert()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            Status = PaymentStatus.Paid,
            PaidOnline = true,
            MpPaymentId = 704,
        });
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var response = await client.PostAsync($"/api/payments/{seed.PaymentId}/refund-duplicate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PAYMENT_NOT_REFUNDABLE", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    // ── Regras do PaymentService ─────────────────────────────────────────────

    [Fact]
    public async Task Payment_update_blocks_plan_change_with_pending_online_payment_and_delete_blocks_paid_online()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            MpPaymentId = 800,
            MpStatus = "pending",
        });
        var otherPlanId = await MercadoPagoTestSupport.SeedPlanAsync(app, seed.ClinicaId, "Outro plano", 300m);
        using var client = await MercadoPagoTestSupport.LoginAdminAsync(app, "mp-admin@test.local");

        var update = await client.PutAsJsonAsync($"/api/payments/{seed.PaymentId}", new
        {
            planId = otherPlanId,
            referenceMonth = "2026-09-01",
            paymentMethod = "Pix",
            status = "Pending",
            paidAt = (string?)null,
            paymentDate = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        // PaymentsController não expõe o code neste endpoint (só a mensagem).
        Assert.Contains("pagamento online em andamento", await update.Content.ReadAsStringAsync());

        var paidOnlineId = 0;
        await app.SeedAsync(async db =>
        {
            var payment = new Payment
            {
                ClinicaId = seed.ClinicaId,
                UserId = seed.AdminId,
                PatientId = seed.PatientId,
                PlanId = seed.PlanId,
                ReferenceMonth = new DateOnly(2026, 10, 1),
                Amount = 150m,
                PaymentMethod = "Pix",
                Status = PaymentStatus.Paid,
                PaidOnline = true,
                MpPaymentId = 801,
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            paidOnlineId = payment.Id;
        });

        var delete = await client.DeleteAsync($"/api/payments/{paidOnlineId}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Contains("paga online", await delete.Content.ReadAsStringAsync());
    }

    // ── Portal do paciente ───────────────────────────────────────────────────

    [Fact]
    public async Task Patient_portal_lists_only_own_charges_from_connected_clinics_pending_first()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var accountA = await MercadoPagoTestSupport.SeedPatientAccountAsync(app, "a@example.com");
        var accountB = await MercadoPagoTestSupport.SeedPatientAccountAsync(app, "b@example.com");

        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            AdminEmail = "a1@test.local",
            ClinicName = "Clínica A",
            PatientAccountId = accountA,
            PatientEmail = "a@example.com",
            PaymentLinkToken = "tok-a",
            ReferenceMonth = new DateOnly(2026, 9, 1),
        });
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            AdminEmail = "a2@test.local",
            ClinicName = "Clínica B",
            PatientAccountId = accountA,
            PatientEmail = "a@example.com",
            Status = PaymentStatus.Paid,
            PaidOnline = true,
            PaidAt = new DateOnly(2026, 9, 10),
            MpPaymentId = 900,
            PaymentLinkToken = "tok-b",
            ReferenceMonth = new DateOnly(2026, 8, 1),
        });
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            AdminEmail = "a3@test.local",
            ClinicName = "Clínica sem MP",
            ConnectMp = false,
            PatientAccountId = accountA,
            PatientEmail = "a@example.com",
            PaymentLinkToken = "tok-c",
            ReferenceMonth = new DateOnly(2026, 7, 1),
        });
        await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            AdminEmail = "b1@test.local",
            ClinicName = "Clínica de outro paciente",
            MpUserId = 888,
            PatientAccountId = accountB,
            PatientEmail = "b@example.com",
            PaymentLinkToken = "tok-d",
            ReferenceMonth = new DateOnly(2026, 9, 1),
        });

        using var client = await MercadoPagoTestSupport.LoginPatientAsync(app, "a@example.com");
        var body = await ReadJsonAsync(await client.GetAsync("/api/patient/payments"));

        Assert.Equal(2, body.GetArrayLength());
        var pending = body[0];
        Assert.Equal("Clínica A", pending.GetProperty("clinicName").GetString());
        Assert.Equal("Pending", pending.GetProperty("status").GetString());
        Assert.Equal(150m, pending.GetProperty("amount").GetDecimal());
        Assert.Equal("2026-09-01", pending.GetProperty("referenceMonth").GetString());
        Assert.Equal("Mensal", pending.GetProperty("planName").GetString());
        Assert.Equal("https://cliniqcare.com.br/pagar/tok-a", pending.GetProperty("payUrl").GetString());

        var paid = body[1];
        Assert.Equal("Clínica B", paid.GetProperty("clinicName").GetString());
        Assert.Equal("Paid", paid.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Patient_portal_generates_a_token_when_the_charge_has_none()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = MercadoPagoTestSupport.CreateApp(fake);
        var account = await MercadoPagoTestSupport.SeedPatientAccountAsync(app, "sem-token@example.com");
        var seed = await MercadoPagoTestSupport.SeedClinicAsync(app, new ClinicSeedOptions
        {
            PatientAccountId = account,
            PatientEmail = "sem-token@example.com",
        });

        using var client = await MercadoPagoTestSupport.LoginPatientAsync(app, "sem-token@example.com");
        var body = await ReadJsonAsync(await client.GetAsync("/api/patient/payments"));

        Assert.Equal(1, body.GetArrayLength());
        Assert.Contains("/pagar/", body[0].GetProperty("payUrl").GetString());
        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(item => item.Id == seed.PaymentId);
            Assert.NotNull(payment.PaymentLinkToken);
        });
    }

    // ── E-mail ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Outbox_processor_sends_the_payment_charge_email()
    {
        var email = new PatientImportEmailSender();
        await using var app = new PatientImportFactory(emailSender: email);
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica MP", NomeFantasia = "Clínica MP", NomeResponsavel = "Admin" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = "mp-admin@test.local",
                PasswordHash = "hash",
                Role = UserRole.Administrador,
            };
            var patient = new Patient
            {
                ClinicaId = clinic.Id,
                Name = "Ana",
                Email = "ana@example.com",
                IsActive = true,
            };
            var sessionType = new SessionType { ClinicaId = clinic.Id, Name = "Fisioterapia" };
            db.Users.Add(admin);
            db.Patients.Add(patient);
            db.SessionTypes.Add(sessionType);
            await db.SaveChangesAsync();

            var plan = new Plans
            {
                ClinicaId = clinic.Id,
                Name = "Mensal",
                Valor = 150m,
                TipoPlano = TipoPlano.Mensal,
                TipoSessaoId = sessionType.Id,
            };
            db.Plans.Add(plan);
            await db.SaveChangesAsync();

            var payment = new Payment
            {
                ClinicaId = clinic.Id,
                UserId = admin.Id,
                PatientId = patient.Id,
                PlanId = plan.Id,
                ReferenceMonth = new DateOnly(2026, 9, 1),
                Amount = 150m,
                PaymentMethod = "Pix",
                Status = PaymentStatus.Pending,
                PaymentLinkToken = "tok-email",
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            db.PatientEmailOutbox.Add(new PatientEmailOutbox
            {
                ClinicaId = clinic.Id,
                PatientId = patient.Id,
                PatientAccountId = null,
                PaymentId = payment.Id,
                Type = PatientEmailOutboxType.PaymentCharge,
                DeduplicationKey = "payment-charge:test",
                NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
            });
            await db.SaveChangesAsync();
        });

        using var scope = app.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<PatientEmailOutboxProcessor>();
        Assert.Equal(1, await processor.ProcessBatchAsync());

        var message = Assert.Single(email.Messages);
        Assert.Equal("ana@example.com", message.To);
        Assert.Equal("Cobrança de Clínica MP", message.Subject);
        Assert.Contains("R$", message.Body);
        Assert.Contains("Mensal", message.Body);
        Assert.Contains("/pagar/tok-email", message.Body);

        await app.SeedAsync(async db =>
        {
            var job = await db.PatientEmailOutbox.SingleAsync();
            Assert.Equal(PatientEmailOutboxType.PaymentCharge, job.Type); // não vira ActivationInvitation
            Assert.Equal(PatientEmailOutboxStatus.Completed, job.Status);
            Assert.Null(job.LastError);
        });
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private static Dictionary<string, object?> Payload(
        string paymentMethodId,
        string payerEmail,
        string? token = null,
        int? installments = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["payment_method_id"] = paymentMethodId,
            ["payer"] = new Dictionary<string, object?> { ["email"] = payerEmail },
        };
        if (token is not null)
            payload["token"] = token;
        if (installments is not null)
            payload["installments"] = installments;
        return payload;
    }

    private static MpPayment Mp(
        long id,
        int paymentId,
        string status,
        string? detail = null,
        long collectorId = MercadoPagoTestSupport.DefaultMpUserId,
        string? externalReference = null,
        string? type = "bank_transfer",
        DateTime? dateApproved = null)
        => new(id, status, detail, externalReference ?? $"payment:{paymentId}", collectorId, type, "pix", dateApproved);

    private static async Task<Payment?> ApplyAsync(MultiClinicaFactory app, int clinicaId, long mpPaymentId)
    {
        using var scope = app.Services.CreateScope();
        var sync = scope.ServiceProvider.GetRequiredService<MercadoPagoPaymentSync>();
        return await sync.ApplyAsync(clinicaId, mpPaymentId);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
