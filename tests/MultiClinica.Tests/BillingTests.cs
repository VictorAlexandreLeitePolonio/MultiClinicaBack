using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Billing;
using Xunit;

namespace MultiClinica.Tests;

public class BillingTests
{
    private const string Password = "senha-super-forte";
    private const string AdminEmail = "billing-admin@test.local";
    private const string WebhookSecret = "s3cret";
    private const string HmacKey = "k";

    private static MultiClinicaFactory CreateApp(FakeAbacatePayClient fake) => new()
    {
        ConfigureTestServices = services =>
        {
            services.RemoveAll<IAbacatePayClient>();
            services.AddSingleton<IAbacatePayClient>(fake);
            services.RemoveAll<IConfigureOptions<AbacatePayOptions>>();
            services.Configure<AbacatePayOptions>(options =>
            {
                options.WebhookSecret = WebhookSecret;
                options.WebhookHmacKey = HmacKey;
                options.Products = new Dictionary<string, string>
                {
                    ["Pro_Monthly"] = "prod_pro_m",
                    ["Premium_Annual"] = "prod_prem_a",
                };
            });
        },
    };

    private static async Task<int> SeedClinicAsync(
        MultiClinicaFactory app,
        ClinicPlan plan = ClinicPlan.Free,
        bool founder = false,
        int extras = 0,
        string? customerId = null,
        string? subscriptionId = null,
        int professionals = 0)
    {
        var id = 0;
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = "Clínica Teste",
                NomeFantasia = "Clínica Teste",
                NomeResponsavel = "Admin",
                Email = $"clinic-{AdminEmail}",
                Telefone = "11999999999",
                Plan = plan,
                IsFounder = founder,
                ExtraProfessionals = extras,
                AbacateCustomerId = customerId,
                AbacateSubscriptionId = subscriptionId,
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            id = clinic.Id;

            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Administrador,
            });
            for (var i = 0; i < professionals; i++)
                db.Users.Add(new User
                {
                    ClinicaId = clinic.Id,
                    Name = $"P{i}",
                    Email = $"p{i}@{AdminEmail}",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                    Role = UserRole.Profissional,
                    CreatedAt = DateTime.UtcNow.AddMinutes(i),
                });
            await db.SaveChangesAsync();
        });
        return id;
    }

    private static async Task<HttpClient> LoginAdminAsync(MultiClinicaFactory app)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login",
            new { email = AdminEmail, password = Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static string Sign(string body, string key = HmacKey) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(body)));

    private static Task<HttpResponseMessage> PostWebhookAsync(
        HttpClient client, string body, string secret = WebhookSecret, string? signature = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/billing/webhooks/abacatepay?webhookSecret={Uri.EscapeDataString(secret)}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Webhook-Signature", signature ?? Sign(body));
        return client.SendAsync(request);
    }

    private static string SubscriptionCompletedPayload(int clinicId, string eventId = "log_sub_1") => $$"""
        {
          "id": "{{eventId}}",
          "event": "subscription.completed",
          "apiVersion": 2,
          "devMode": false,
          "data": {
            "subscription": {
              "id": "subs_1", "amount": 79900, "currency": "BRL", "method": "CARD",
              "status": "ACTIVE", "frequency": "ANNUALLY"
            },
            "customer": { "id": "cust_1", "name": "Clínica Teste", "email": "admin@teste.com" },
            "payment": { "id": "char_1", "externalId": "clinica:{{clinicId}}" },
            "checkout": {
              "id": "bill_1", "externalId": "clinica:{{clinicId}}", "frequency": "SUBSCRIPTION",
              "items": [{ "id": "prod_prem_a", "quantity": 1 }],
              "status": "PAID", "customerId": "cust_1"
            }
          }
        }
        """;

    private static string SubscriptionCancelledPayload(int clinicId, string eventId = "log_sub_2") => $$"""
        {
          "id": "{{eventId}}",
          "event": "subscription.cancelled",
          "apiVersion": 2,
          "devMode": false,
          "data": {
            "subscription": {
              "id": "subs_1", "amount": 79900, "currency": "BRL", "method": "CARD",
              "status": "CANCELLED", "frequency": "ANNUALLY", "cancelledDueTo": null
            },
            "customer": { "id": "cust_1", "name": "Clínica Teste", "email": "admin@teste.com" },
            "checkout": {
              "id": "bill_1", "externalId": "clinica:{{clinicId}}", "frequency": "SUBSCRIPTION",
              "items": [{ "id": "prod_prem_a", "quantity": 1 }], "status": "PAID"
            }
          }
        }
        """;

    private static string CheckoutCompletedPayload(int clinicId, string item, string eventId) => $$"""
        {
          "id": "{{eventId}}",
          "event": "checkout.completed",
          "apiVersion": 2,
          "devMode": false,
          "data": {
            "checkout": {
              "id": "bill_3", "externalId": "clinica:{{clinicId}}|{{item}}",
              "frequency": "ONE_TIME", "items": [{ "id": "prod_pack", "quantity": 1 }],
              "status": "PAID", "customerId": "cust_3"
            },
            "customer": { "id": "cust_3", "name": "Clínica Teste", "email": "admin@teste.com" }
          }
        }
        """;

    [Fact]
    public async Task Me_returns_free_plan_limits()
    {
        await using var app = CreateApp(new FakeAbacatePayClient());
        await SeedClinicAsync(app);
        using var client = await LoginAdminAsync(app);

        var response = await client.GetAsync("/api/billing/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Free", body.GetProperty("plan").GetString());
        Assert.Equal(1, body.GetProperty("professionalLimit").GetInt32());
        Assert.False(body.GetProperty("hasSubscription").GetBoolean());
    }

    [Fact]
    public async Task Subscription_checkout_returns_abacatepay_url_and_sends_product()
    {
        var fake = new FakeAbacatePayClient();
        await using var app = CreateApp(fake);
        var clinicId = await SeedClinicAsync(app);
        using var client = await LoginAdminAsync(app);

        var response = await client.PostAsJsonAsync("/api/billing/subscription/checkout",
            new { plan = "Pro", cycle = "Monthly" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("https://pay.test/checkout", body.GetProperty("url").GetString());
        Assert.Equal("prod_pro_m", fake.LastProductId);
        Assert.Equal(clinicId, fake.LastClinicaId);
        Assert.Null(fake.LastCustomerId);
    }

    [Fact]
    public async Task Subscription_checkout_reuses_existing_customer_id()
    {
        var fake = new FakeAbacatePayClient();
        await using var app = CreateApp(fake);
        await SeedClinicAsync(app, customerId: "cust_existente");
        using var client = await LoginAdminAsync(app);

        var response = await client.PostAsJsonAsync("/api/billing/subscription/checkout",
            new { plan = "Pro", cycle = "Monthly" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("cust_existente", fake.LastCustomerId);
    }

    [Fact]
    public async Task Founder_checkout_returns_conflict()
    {
        await using var app = CreateApp(new FakeAbacatePayClient());
        await SeedClinicAsync(app, ClinicPlan.Premium, founder: true);
        using var client = await LoginAdminAsync(app);

        var response = await client.PostAsJsonAsync("/api/billing/subscription/checkout",
            new { plan = "Pro", cycle = "Monthly" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("fundadora", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Webhook_rejects_wrong_secret_and_wrong_hmac()
    {
        await using var app = CreateApp(new FakeAbacatePayClient());
        using var client = app.CreateClient();
        var body = SubscriptionCompletedPayload(1);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await PostWebhookAsync(client, body, secret: "errado")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await PostWebhookAsync(client, body, signature: Sign(body, "outra-chave"))).StatusCode);
    }

    [Fact]
    public async Task Subscription_completed_updates_plan_cycle_and_customer()
    {
        await using var app = CreateApp(new FakeAbacatePayClient());
        var clinicId = await SeedClinicAsync(app);
        using var client = app.CreateClient();

        var response = await PostWebhookAsync(client, SubscriptionCompletedPayload(clinicId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var clinic = await db.Clinicas.SingleAsync(item => item.Id == clinicId);
            Assert.Equal(ClinicPlan.Premium, clinic.Plan);
            Assert.Equal(BillingCycle.Annual, clinic.BillingCycle);
            Assert.Equal("subs_1", clinic.AbacateSubscriptionId);
            Assert.Equal("cust_1", clinic.AbacateCustomerId);
        });
    }

    [Fact]
    public async Task Repeated_webhook_event_is_applied_once()
    {
        await using var app = CreateApp(new FakeAbacatePayClient());
        var clinicId = await SeedClinicAsync(app);
        using var client = app.CreateClient();
        var body = SubscriptionCompletedPayload(clinicId);

        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(client, body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(client, body)).StatusCode);

        await app.SeedAsync(async db =>
        {
            Assert.Equal(1, await db.ProcessedWebhookEvents.CountAsync());
            Assert.Equal(1, await db.CommercialHistoryEvents.CountAsync(item =>
                item.ClinicaId == clinicId && item.Type == CommercialHistoryEventType.PlanChanged));
        });
    }

    [Fact]
    public async Task Subscription_cancelled_downgrades_and_deactivates_most_recent_professionals()
    {
        await using var app = CreateApp(new FakeAbacatePayClient());
        var clinicId = await SeedClinicAsync(app, ClinicPlan.Premium,
            subscriptionId: "subs_1", professionals: 3);
        using var client = app.CreateClient();

        var response = await PostWebhookAsync(client, SubscriptionCancelledPayload(clinicId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var clinic = await db.Clinicas.SingleAsync(item => item.Id == clinicId);
            Assert.Equal(ClinicPlan.Free, clinic.Plan);
            Assert.Null(clinic.AbacateSubscriptionId);
            var active = await db.Users
                .Where(item => item.ClinicaId == clinicId && item.Role == UserRole.Profissional && item.IsActive)
                .Select(item => item.Name).ToListAsync();
            Assert.Equal(["P0"], active);
        });
    }

    [Fact]
    public async Task Checkout_completed_pack_grants_usage_and_persists_customer()
    {
        await using var app = CreateApp(new FakeAbacatePayClient());
        var clinicId = await SeedClinicAsync(app, ClinicPlan.Pro);
        using var client = await LoginAdminAsync(app);

        var response = await PostWebhookAsync(client,
            CheckoutCompletedPayload(clinicId, "pack:pro-conversations-200", "log_pack_1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var usage = JsonDocument.Parse(
            await (await client.GetAsync("/api/billing/usage")).Content.ReadAsStringAsync()).RootElement;
        var conversations = usage.GetProperty("metrics").EnumerateArray()
            .Single(metric => metric.GetProperty("metric").GetString() == "WhatsAppConversations");
        Assert.Equal(200, conversations.GetProperty("extraGranted").GetInt32());
        await app.SeedAsync(async db =>
        {
            var clinic = await db.Clinicas.SingleAsync(item => item.Id == clinicId);
            Assert.Equal("cust_3", clinic.AbacateCustomerId);
        });
    }

    [Fact]
    public async Task Extra_professional_purchase_above_plan_limit_returns_conflict()
    {
        await using var app = CreateApp(new FakeAbacatePayClient());
        await SeedClinicAsync(app, ClinicPlan.Free, extras: 1);
        using var client = await LoginAdminAsync(app);

        var response = await client.PostAsJsonAsync("/api/billing/purchase",
            new { item = "extra-professional" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed class FakeAbacatePayClient : IAbacatePayClient
    {
        public string? LastProductId { get; private set; }
        public int LastClinicaId { get; private set; }
        public string? LastCustomerId { get; private set; }
        public string? LastSubscriptionId { get; private set; }
        public string Url { get; init; } = "https://pay.test/checkout";

        public Task<string> CreateSubscriptionCheckoutAsync(
            string productId, int clinicaId, AbacateCustomer customer, CancellationToken ct = default)
        {
            LastProductId = productId;
            LastClinicaId = clinicaId;
            LastCustomerId = customer.ExistingId;
            return Task.FromResult(Url);
        }

        public Task<string> CreateOneTimeCheckoutAsync(
            int clinicaId, string item, string description, decimal amount, AbacateCustomer customer,
            CancellationToken ct = default)
        {
            LastClinicaId = clinicaId;
            LastCustomerId = customer.ExistingId;
            return Task.FromResult(Url);
        }

        public Task ChangeSubscriptionAsync(string subscriptionId, string productId, CancellationToken ct = default)
        {
            LastSubscriptionId = subscriptionId;
            return Task.CompletedTask;
        }

        public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default)
        {
            LastSubscriptionId = subscriptionId;
            return Task.CompletedTask;
        }
    }
}
