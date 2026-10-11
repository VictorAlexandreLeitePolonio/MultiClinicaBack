using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class TelegramTests
{
    private const string Secret = "test-secret";

    private static MultiClinicaFactory CreateApp(ITelegramClient telegram) => new()
    {
        ConfigureTestServices = s =>
        {
            s.AddSingleton<ITelegramClient>(telegram);
            s.Configure<TelegramOptions>(o => { o.WebhookSecret = Secret; o.BotUsername = "TestBot"; });
        },
    };

    private static Task<HttpResponseMessage> PostUpdateAsync(HttpClient client, long chatId, int messageId,
        string? text, string? secret = Secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/telegram")
        {
            Content = JsonContent.Create(new
            {
                update_id = messageId,
                message = new { message_id = messageId, chat = new { id = chatId }, text },
            }),
        };
        if (secret is not null) request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        return client.SendAsync(request);
    }

    private static async Task LoginAsync(HttpClient client, string email)
        => (await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = "secret123" }))
            .EnsureSuccessStatusCode();

    [Fact]
    public async Task Webhook_rejects_missing_or_wrong_secret()
    {
        var telegram = new FakeTelegramClient();
        await using var app = CreateApp(telegram);
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostUpdateAsync(client, 1, 1, "oi", secret: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostUpdateAsync(client, 1, 1, "oi", secret: "errado")).StatusCode);
    }

    [Fact]
    public async Task Admin_links_account_through_start_code()
    {
        var telegram = new FakeTelegramClient();
        await using var app = CreateApp(telegram);
        var (_, adminId) = await AgentSeed.ClinicWithAdminAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "agent-admin@test.local");

        var link = await client.PostAsync("/api/me/telegram-link", null);
        link.EnsureSuccessStatusCode();
        var url = JsonDocument.Parse(await link.Content.ReadAsStringAsync()).RootElement.GetProperty("url").GetString()!;
        Assert.StartsWith("https://t.me/TestBot?start=", url);
        var code = url.Split("start=")[1];

        Assert.Equal(HttpStatusCode.OK, (await PostUpdateAsync(client, 4242, 1, $"/start {code}")).StatusCode);

        await app.SeedAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Id == adminId);
            Assert.Equal(4242L, admin.TelegramChatId);
            Assert.Null(admin.TelegramLinkCodeHash);
        });
        Assert.Contains(telegram.Sent, m => m.ChatId == 4242 && m.Text.Contains("Victor"));

        var status = await client.GetFromJsonAsync<JsonElement>("/api/me/telegram-link");
        Assert.True(status.GetProperty("connected").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/me/telegram-link")).StatusCode);
        status = await client.GetFromJsonAsync<JsonElement>("/api/me/telegram-link");
        Assert.False(status.GetProperty("connected").GetBoolean());
    }

    [Fact]
    public async Task Expired_or_unknown_code_does_not_link()
    {
        var telegram = new FakeTelegramClient();
        await using var app = CreateApp(telegram);
        var (_, adminId) = await AgentSeed.ClinicWithAdminAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "agent-admin@test.local");
        var url = JsonDocument.Parse(await (await client.PostAsync("/api/me/telegram-link", null)).Content.ReadAsStringAsync())
            .RootElement.GetProperty("url").GetString()!;
        await app.SeedAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Id == adminId);
            admin.TelegramLinkExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        var first = await PostUpdateAsync(client, 4242, 1, $"/start {url.Split("start=")[1]}");
        var second = await PostUpdateAsync(client, 4242, 2, "/start codigo-inventado");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, telegram.Sent.Count);
        await app.SeedAsync(async db => Assert.Null((await db.Users.SingleAsync(u => u.Id == adminId)).TelegramChatId));
        Assert.All(telegram.Sent, m => Assert.Contains("inválido ou expirado", m.Text));
    }

    [Fact]
    public async Task Linked_user_message_is_queued_once()
    {
        var telegram = new FakeTelegramClient();
        await using var app = CreateApp(telegram);
        await AgentSeed.ClinicWithAdminAsync(app, telegramChatId: 4242);
        using var client = app.CreateClient();

        await PostUpdateAsync(client, 4242, 10, "agenda a cida terça 9h");
        await PostUpdateAsync(client, 4242, 10, "agenda a cida terça 9h"); // reentrega do Telegram

        await app.SeedAsync(async db =>
        {
            var message = await db.AgentMessages.Include(m => m.Conversation).SingleAsync();
            Assert.Equal(AgentMessageStatus.Pending, message.Status);
            Assert.Equal("4242:10", message.ExternalMessageId);
            Assert.Equal(AgentProfile.Owner, message.Conversation.Profile);
        });
        Assert.Empty(telegram.Sent);
    }

    [Fact]
    public async Task Linked_user_message_starting_with_start_word_is_queued_as_text()
    {
        var telegram = new FakeTelegramClient();
        await using var app = CreateApp(telegram);
        await AgentSeed.ClinicWithAdminAsync(app, telegramChatId: 4242);
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await PostUpdateAsync(client, 4242, 20, "/startar agenda")).StatusCode);

        await app.SeedAsync(async db =>
        {
            var message = await db.AgentMessages.SingleAsync();
            Assert.Equal(AgentMessageStatus.Pending, message.Status);
            Assert.Equal(AgentMessageRole.User, message.Role);
            Assert.Equal("/startar agenda", message.Content);
        });
        Assert.Empty(telegram.Sent);
    }

    [Fact]
    public async Task Unlinked_chat_and_non_text_get_guidance_without_queueing()
    {
        var telegram = new FakeTelegramClient();
        await using var app = CreateApp(telegram);
        await AgentSeed.ClinicWithAdminAsync(app, telegramChatId: 4242);
        using var client = app.CreateClient();

        await PostUpdateAsync(client, 9999, 1, "oi");
        await PostUpdateAsync(client, 4242, 2, text: null); // áudio, foto etc.

        await app.SeedAsync(async db => Assert.Empty(await db.AgentMessages.ToListAsync()));
        Assert.Contains(telegram.Sent, m => m.ChatId == 9999 && m.Text.Contains("Conectar Telegram"));
        Assert.Contains(telegram.Sent, m => m.ChatId == 4242 && m.Text.Contains("texto"));
    }

    [Fact]
    public async Task Only_administrators_can_create_links()
    {
        var telegram = new FakeTelegramClient();
        await using var app = CreateApp(telegram);
        var (clinicId, _) = await AgentSeed.ClinicWithAdminAsync(app);
        await app.SeedAsync(async db =>
        {
            db.Users.Add(new User { ClinicaId = clinicId, Name = "Pro", Email = "pro@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Profissional });
            await db.SaveChangesAsync();
        });
        using var client = app.CreateClient();
        await LoginAsync(client, "pro@test.local");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/me/telegram-link", null)).StatusCode);
    }

    [Fact]
    public async Task Relinking_chat_moves_it_from_previous_user()
    {
        var telegram = new FakeTelegramClient();
        await using var app = CreateApp(telegram);
        var (clinicId, previousId) = await AgentSeed.ClinicWithAdminAsync(app, telegramChatId: 4242);
        await app.SeedAsync(async db =>
        {
            db.Users.Add(new User { ClinicaId = clinicId, Name = "Outro", Email = "outro@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Administrador });
            await db.SaveChangesAsync();
        });
        using var client = app.CreateClient();
        await LoginAsync(client, "outro@test.local");
        var url = JsonDocument.Parse(await (await client.PostAsync("/api/me/telegram-link", null)).Content.ReadAsStringAsync())
            .RootElement.GetProperty("url").GetString()!;

        await PostUpdateAsync(client, 4242, 1, $"/start {url.Split("start=")[1]}");

        await app.SeedAsync(async db =>
        {
            Assert.Null((await db.Users.SingleAsync(u => u.Id == previousId)).TelegramChatId);
            Assert.Equal(4242L, (await db.Users.SingleAsync(u => u.Email == "outro@test.local")).TelegramChatId);
        });
    }

    [Fact]
    public async Task Link_completes_even_when_telegram_replies_fail()
    {
        await using var app = CreateApp(new ThrowingTelegramClient());
        var (_, adminId) = await AgentSeed.ClinicWithAdminAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "agent-admin@test.local");
        var url = JsonDocument.Parse(await (await client.PostAsync("/api/me/telegram-link", null)).Content.ReadAsStringAsync())
            .RootElement.GetProperty("url").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await PostUpdateAsync(client, 4242, 1, $"/start {url.Split("start=")[1]}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostUpdateAsync(client, 4242, 2, "/start codigo-inventado")).StatusCode);

        await app.SeedAsync(async db => Assert.Equal(4242L, (await db.Users.SingleAsync(u => u.Id == adminId)).TelegramChatId));
    }
}

public sealed class ThrowingTelegramClient : ITelegramClient
{
    public Task SendMessageAsync(long chatId, string text, CancellationToken ct)
        => Task.FromException(new HttpRequestException("Telegram fora do ar"));
}
