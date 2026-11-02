using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class WhatsAppTests
{
    private const string AppSecret = "app-secret";
    private const string VerifyToken = "verify-token";
    private const string PlatformNumberId = "PNID";
    private const string Phone = "5511999998888";

    private static MultiClinicaFactory CreateApp(FakeWhatsAppClient whatsApp, TimeProvider? clock = null,
        ILoggerProvider? loggerProvider = null) => new()
    {
        ConfigureTestServices = s =>
        {
            s.AddSingleton<IWhatsAppClient>(whatsApp);
            if (clock is not null) s.AddSingleton<TimeProvider>(clock);
            if (loggerProvider is not null) s.AddSingleton(loggerProvider);
            s.Configure<WhatsAppOptions>(o =>
            {
                o.AppSecret = AppSecret;
                o.VerifyToken = VerifyToken;
                o.PhoneNumberId = PlatformNumberId;
                o.DisplayNumber = "5511900000000";
            });
        },
    };

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string from, string wamid, string? text,
        string type = "text", string phoneNumberId = PlatformNumberId, string? signature = null, bool sign = true,
        bool includeStatuses = false, bool includeMessage = true, string status = "delivered",
        object[]? errors = null)
    {
        var json = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    id = "WABA",
                    changes = new[]
                    {
                        new
                        {
                            field = "messages",
                            value = new
                            {
                                messaging_product = "whatsapp",
                                metadata = new { display_phone_number = "5511900000000", phone_number_id = phoneNumberId },
                                messages = includeMessage
                                    ? new[]
                                    {
                                        new
                                        {
                                            from,
                                            id = wamid,
                                            timestamp = "1",
                                            type,
                                            text = text is null ? null : new { body = text },
                                        },
                                    }
                                    : null,
                                statuses = includeStatuses
                                    ? new[]
                                    {
                                        new
                                        {
                                            id = wamid,
                                            status,
                                            timestamp = "1",
                                            recipient_id = from,
                                            errors,
                                        },
                                    }
                                    : null,
                            },
                        },
                    },
                },
            },
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new("application/json");
        if (sign)
            request.Headers.Add("X-Hub-Signature-256", signature
                ?? "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(AppSecret), bytes)));
        return client.SendAsync(request);
    }

    private static async Task LoginAsync(HttpClient client, string email)
        => (await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = "secret123" }))
            .EnsureSuccessStatusCode();

    private static async Task<string> CreateLinkCodeAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/me/whatsapp-link", null);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var url = body.RootElement.GetProperty("url").GetString()!;
        Assert.StartsWith("https://wa.me/5511900000000?text=", url);
        var message = Uri.UnescapeDataString(url.Split("text=")[1]);
        Assert.StartsWith("conectar ", message);
        return message["conectar ".Length..].Trim();
    }

    [Fact]
    public async Task Verify_returns_challenge_only_with_right_token()
    {
        await using var app = CreateApp(new FakeWhatsAppClient());
        using var client = app.CreateClient();

        var ok = await client.GetAsync(
            $"/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token={VerifyToken}&hub.challenge=abc123");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("abc123", await ok.Content.ReadAsStringAsync());
        Assert.StartsWith("text/plain", ok.Content.Headers.ContentType!.ToString());

        var wrong = await client.GetAsync(
            "/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=abc123");
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
    }

    [Fact]
    public async Task Webhook_rejects_missing_or_wrong_signature()
    {
        await using var app = CreateApp(new FakeWhatsAppClient());
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await PostAsync(client, Phone, "wamid.1", "oi", signature: "sha256=00")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await PostAsync(client, Phone, "wamid.2", "oi", sign: false)).StatusCode);
    }

    [Fact]
    public async Task Other_phone_number_ids_are_ignored()
    {
        var whatsApp = new FakeWhatsAppClient();
        await using var app = CreateApp(whatsApp);
        await AgentSeed.ClinicWithAdminAsync(app, whatsAppPhone: Phone);
        using var client = app.CreateClient();

        await PostAsync(client, Phone, "wamid.1", "oi", phoneNumberId: "OTHER");
        await PostAsync(client, Phone, "wamid.2", "oi", includeStatuses: true, includeMessage: false);

        await app.SeedAsync(async db =>
        {
            Assert.Empty(await db.AgentMessages.ToListAsync());
            Assert.Empty(await db.AgentConversations.ToListAsync());
        });
        Assert.Empty(whatsApp.Sent);
    }

    [Fact]
    public async Task Failed_status_is_logged_without_recipient()
    {
        var logs = new CapturingLoggerProvider();
        await using var app = CreateApp(new FakeWhatsAppClient(), loggerProvider: logs);
        using var client = app.CreateClient();

        var response = await PostAsync(client, Phone, "wamid.falha", text: null, includeStatuses: true,
            includeMessage: false, status: "failed",
            errors: [new { code = 130497, title = "Business account is restricted from messaging users in this country" }]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var warning = Assert.Single(logs.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("entrega falhou"));
        Assert.Contains("130497", warning.Message);
        Assert.Contains("wamid.falha", warning.Message);
        Assert.DoesNotContain(Phone, warning.Message);
    }

    [Fact]
    public async Task Delivered_status_is_not_logged_as_failure()
    {
        var logs = new CapturingLoggerProvider();
        await using var app = CreateApp(new FakeWhatsAppClient(), loggerProvider: logs);
        using var client = app.CreateClient();

        var response = await PostAsync(client, Phone, "wamid.entrega", text: null, includeStatuses: true,
            includeMessage: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("entrega falhou"));
    }

    [Fact]
    public async Task Admin_links_whatsapp_through_code()
    {
        var whatsApp = new FakeWhatsAppClient();
        await using var app = CreateApp(whatsApp);
        var (_, adminId) = await AgentSeed.ClinicWithAdminAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "agent-admin@test.local");
        var code = await CreateLinkCodeAsync(client);

        Assert.Equal(HttpStatusCode.OK,
            (await PostAsync(client, Phone, "wamid.1", $"conectar {code}")).StatusCode);

        await app.SeedAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Id == adminId);
            Assert.Equal(Phone, admin.WhatsAppPhone);
            Assert.Null(admin.WhatsAppLinkCodeHash);
            Assert.Null(admin.WhatsAppLinkExpiresAt);
        });
        Assert.Contains(whatsApp.Sent, m => m.To == Phone && m.Text.Contains("Victor"));

        var status = await client.GetFromJsonAsync<JsonElement>("/api/me/whatsapp-link");
        Assert.True(status.GetProperty("connected").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/me/whatsapp-link")).StatusCode);
        status = await client.GetFromJsonAsync<JsonElement>("/api/me/whatsapp-link");
        Assert.False(status.GetProperty("connected").GetBoolean());
    }

    [Fact]
    public async Task Expired_or_reused_code_does_not_link()
    {
        var whatsApp = new FakeWhatsAppClient();
        await using var app = CreateApp(whatsApp);
        var (_, adminId) = await AgentSeed.ClinicWithAdminAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "agent-admin@test.local");
        var expired = await CreateLinkCodeAsync(client);
        await app.SeedAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Id == adminId);
            admin.WhatsAppLinkExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        await PostAsync(client, Phone, "wamid.1", $"conectar {expired}");
        await app.SeedAsync(async db =>
            Assert.Null((await db.Users.SingleAsync(u => u.Id == adminId)).WhatsAppPhone));

        var valid = await CreateLinkCodeAsync(client);
        await PostAsync(client, Phone, "wamid.2", $"conectar {valid}");
        await app.SeedAsync(async db =>
            Assert.Equal(Phone, (await db.Users.SingleAsync(u => u.Id == adminId)).WhatsAppPhone));

        await PostAsync(client, Phone, "wamid.3", $"conectar {valid}"); // reutilizado
        await app.SeedAsync(async db =>
            Assert.Equal(Phone, (await db.Users.SingleAsync(u => u.Id == adminId)).WhatsAppPhone));

        Assert.Equal(2, whatsApp.Sent.Count(m => m.Text.Contains("inválido ou expirado")));
    }

    [Fact]
    public async Task Relinking_number_moves_it_from_previous_user()
    {
        var whatsApp = new FakeWhatsAppClient();
        await using var app = CreateApp(whatsApp);
        var (clinicId, previousId) = await AgentSeed.ClinicWithAdminAsync(app, whatsAppPhone: Phone);
        await app.SeedAsync(async db =>
        {
            db.Users.Add(new User
            {
                ClinicaId = clinicId, Name = "Outro", Email = "outro@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Administrador,
            });
            await db.SaveChangesAsync();
        });
        using var client = app.CreateClient();
        await LoginAsync(client, "outro@test.local");
        var code = await CreateLinkCodeAsync(client);

        await PostAsync(client, Phone, "wamid.1", $"conectar {code}");

        await app.SeedAsync(async db =>
        {
            Assert.Null((await db.Users.SingleAsync(u => u.Id == previousId)).WhatsAppPhone);
            Assert.Equal(Phone, (await db.Users.SingleAsync(u => u.Email == "outro@test.local")).WhatsAppPhone);
        });
    }

    [Fact]
    public async Task Link_completes_even_when_replies_fail()
    {
        var whatsApp = new FakeWhatsAppClient { Fail = true };
        await using var app = CreateApp(whatsApp);
        var (_, adminId) = await AgentSeed.ClinicWithAdminAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "agent-admin@test.local");
        var code = await CreateLinkCodeAsync(client);

        Assert.Equal(HttpStatusCode.OK,
            (await PostAsync(client, Phone, "wamid.1", $"conectar {code}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await PostAsync(client, Phone, "wamid.2", "conectar codigo-inventado")).StatusCode);

        await app.SeedAsync(async db =>
            Assert.Equal(Phone, (await db.Users.SingleAsync(u => u.Id == adminId)).WhatsAppPhone));
        Assert.Empty(whatsApp.Sent);
    }

    [Fact]
    public async Task Unknown_number_and_non_text_get_guidance_without_queueing()
    {
        var whatsApp = new FakeWhatsAppClient();
        await using var app = CreateApp(whatsApp);
        await AgentSeed.ClinicWithAdminAsync(app, whatsAppPhone: Phone);
        using var client = app.CreateClient();

        await PostAsync(client, "5511000000000", "wamid.1", "oi");
        await PostAsync(client, Phone, "wamid.2", text: null, type: "audio");

        await app.SeedAsync(async db => Assert.Empty(await db.AgentMessages.ToListAsync()));
        Assert.Contains(whatsApp.Sent, m => m.To == "5511000000000" && m.Text.Contains("Conectar WhatsApp"));
        Assert.Contains(whatsApp.Sent, m => m.To == Phone && m.Text.Contains("só entendo texto"));
    }

    [Fact]
    public async Task Linked_message_is_queued_once()
    {
        var whatsApp = new FakeWhatsAppClient();
        await using var app = CreateApp(whatsApp);
        await AgentSeed.ClinicWithAdminAsync(app, whatsAppPhone: Phone);
        using var client = app.CreateClient();

        await PostAsync(client, Phone, "wamid.1", "agenda a cida terça 9h");
        await PostAsync(client, Phone, "wamid.1", "agenda a cida terça 9h"); // reentrega da Meta

        await app.SeedAsync(async db =>
        {
            var message = await db.AgentMessages.Include(m => m.Conversation).SingleAsync();
            Assert.Equal(AgentMessageStatus.Pending, message.Status);
            Assert.Equal(AgentMessageRole.User, message.Role);
            Assert.Equal("wamid.1", message.ExternalMessageId);
            Assert.Equal(AgentChannel.WhatsApp, message.Channel);
            Assert.Equal(AgentChannel.WhatsApp, message.Conversation.Channel);
            Assert.Equal(AgentProfile.Owner, message.Conversation.Profile);
            Assert.Equal(Phone, message.Conversation.ExternalChatId);
        });
        Assert.Empty(whatsApp.Sent);
    }

    [Fact]
    public async Task Free_plan_gets_upgrade_message()
    {
        var whatsApp = new FakeWhatsAppClient();
        await using var app = CreateApp(whatsApp);
        var (clinicId, _) = await AgentSeed.ClinicWithAdminAsync(app, whatsAppPhone: Phone);
        await app.SeedAsync(async db =>
        {
            (await db.Clinicas.SingleAsync(c => c.Id == clinicId)).Plan = ClinicPlan.Free;
            await db.SaveChangesAsync();
        });
        using var client = app.CreateClient();

        await PostAsync(client, Phone, "wamid.1", "oi");

        await app.SeedAsync(async db => Assert.Empty(await db.AgentMessages.ToListAsync()));
        Assert.Contains(whatsApp.Sent, m => m.To == Phone && m.Text.Contains("a partir do plano Pro"));
    }

    [Fact]
    public async Task First_message_of_window_consumes_one_conversation()
    {
        var whatsApp = new FakeWhatsAppClient();
        var clock = new AgentInboxProcessorTests.ManualClock();
        await using var app = CreateApp(whatsApp, clock);
        var (clinicId, _) = await AgentSeed.ClinicWithAdminAsync(app, whatsAppPhone: Phone);
        using var client = app.CreateClient();

        await PostAsync(client, Phone, "wamid.1", "primeira");
        await PostAsync(client, Phone, "wamid.2", "segunda");
        await app.SeedAsync(async db =>
        {
            var usage = await db.ClinicUsages.SingleAsync(u => u.ClinicaId == clinicId
                && u.Metric == UsageMetric.WhatsAppConversations);
            Assert.Equal(1, usage.Used);
        });

        clock.Advance(TimeSpan.FromHours(25));
        await PostAsync(client, Phone, "wamid.3", "depois de 24h");
        await app.SeedAsync(async db =>
        {
            var usage = await db.ClinicUsages.SingleAsync(u => u.ClinicaId == clinicId
                && u.Metric == UsageMetric.WhatsAppConversations);
            Assert.Equal(2, usage.Used);
            Assert.Equal(3, await db.AgentMessages.CountAsync());
        });
    }

    [Fact]
    public async Task Exhausted_quota_replies_and_does_not_queue()
    {
        var whatsApp = new FakeWhatsAppClient();
        await using var app = CreateApp(whatsApp);
        var (clinicId, _) = await AgentSeed.ClinicWithAdminAsync(app, whatsAppPhone: Phone);
        await app.SeedAsync(async db =>
        {
            db.ClinicUsages.Add(new ClinicUsage
            {
                ClinicaId = clinicId,
                Period = UsageService.CurrentPeriod(DateTime.UtcNow),
                Metric = UsageMetric.WhatsAppConversations,
                Used = 300, // limite do Pro
            });
            await db.SaveChangesAsync();
        });
        using var client = app.CreateClient();

        await PostAsync(client, Phone, "wamid.1", "oi");

        await app.SeedAsync(async db =>
        {
            Assert.Empty(await db.AgentMessages.ToListAsync());
            Assert.Empty(await db.AgentConversations.ToListAsync());
        });
        Assert.Contains(whatsApp.Sent, m => m.To == Phone && m.Text.Contains("cota de conversas do mês acabou"));
    }

    [Fact]
    public async Task Only_administrators_can_create_links()
    {
        var whatsApp = new FakeWhatsAppClient();
        await using var app = CreateApp(whatsApp);
        var (clinicId, _) = await AgentSeed.ClinicWithAdminAsync(app);
        await app.SeedAsync(async db =>
        {
            db.Users.Add(new User
            {
                ClinicaId = clinicId, Name = "Pro", Email = "pro@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Profissional,
            });
            await db.SaveChangesAsync();
        });
        using var client = app.CreateClient();
        await LoginAsync(client, "pro@test.local");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/me/whatsapp-link", null)).StatusCode);
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries
        {
            get { lock (_entries) return _entries.ToList(); }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (provider._entries)
                    provider._entries.Add(new LogEntry(logLevel, formatter(state, exception)));
            }
        }
    }
}
