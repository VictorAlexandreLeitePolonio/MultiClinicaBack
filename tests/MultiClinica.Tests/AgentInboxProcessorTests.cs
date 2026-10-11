using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class AgentInboxProcessorTests
{
    private static async Task<(MultiClinicaFactory App, FakeChatModel Model, FakeTelegramClient Telegram, ManualClock Clock)> SetupAsync(
        bool billingBlocked = false)
    {
        var model = new FakeChatModel();
        var telegram = new FakeTelegramClient();
        var clock = new ManualClock();
        var app = new MultiClinicaFactory
        {
            ConfigureTestServices = s =>
            {
                s.AddSingleton<IChatModel>(model);
                s.AddSingleton<ITelegramClient>(telegram);
                s.AddSingleton<TimeProvider>(clock);
                s.Configure<AgentOptions>(o => o.Model = "modelo-teste");
            },
        };
        var (clinicId, adminId) = await AgentSeed.ClinicWithAdminAsync(app, telegramChatId: 777);
        await app.SeedAsync(async db =>
        {
            var clinic = await db.Clinicas.SingleAsync(c => c.Id == clinicId);
            clinic.IsBlockedByBilling = billingBlocked;
            var conversation = new AgentConversation { ClinicaId = clinicId, Channel = AgentChannel.Telegram,
                Profile = AgentProfile.Owner, ExternalChatId = "777", UserId = adminId, LastInboundAt = DateTime.UtcNow };
            conversation.Messages.Add(new AgentMessage { Channel = AgentChannel.Telegram, Role = AgentMessageRole.User,
                Content = "oi", Status = AgentMessageStatus.Pending, ExternalMessageId = "777:1" });
            db.AgentConversations.Add(conversation);
            await db.SaveChangesAsync();
        });
        return (app, model, telegram, clock);
    }

    private static Task ProcessAsync(MultiClinicaFactory app)
        => app.Services.GetRequiredService<AgentInboxProcessor>().ProcessBatchAsync(CancellationToken.None);

    private static async Task<AgentMessage> InboundAsync(MultiClinicaFactory app, string externalId = "777:1")
    {
        AgentMessage? message = null;
        await app.SeedAsync(async db => message = await db.AgentMessages.SingleAsync(m => m.ExternalMessageId == externalId));
        return message!;
    }

    [Fact]
    public async Task Replies_on_telegram_and_marks_done()
    {
        var (app, model, telegram, _) = await SetupAsync();
        await using var _ = app;
        model.Reply("Olá, Victor!");

        await ProcessAsync(app);

        Assert.Equal([(777L, "Olá, Victor!")], telegram.Sent);
        Assert.Equal(AgentMessageStatus.Done, (await InboundAsync(app)).Status);
    }

    [Fact]
    public async Task Retries_then_gives_up_with_apology()
    {
        var (app, model, telegram, clock) = await SetupAsync();
        await using var _ = app;
        model.Throw().Throw().Throw();

        await ProcessAsync(app);
        var afterFirst = await InboundAsync(app);
        Assert.Equal(AgentMessageStatus.Pending, afterFirst.Status);
        Assert.Equal(1, afterFirst.Attempts);
        Assert.Empty(telegram.Sent);

        // Backoff: sem tempo passar, a mensagem que acabou de falhar não volta ao LLM.
        await ProcessAsync(app);
        Assert.Single(model.Requests);

        // ApplyAudit regrava UpdatedAt no SaveChanges, então o tempo avança pelo TimeProvider, não pelo banco.
        clock.Advance(TimeSpan.FromMinutes(10));
        await ProcessAsync(app);
        clock.Advance(TimeSpan.FromMinutes(10));
        await ProcessAsync(app);

        Assert.Equal(3, model.Requests.Count);
        Assert.Equal(AgentMessageStatus.Failed, (await InboundAsync(app)).Status);
        Assert.Equal([(777L, AgentInboxProcessor.FailureReply)], telegram.Sent);
    }

    [Fact]
    public async Task Blocked_clinic_is_not_processed()
    {
        var (app, model, telegram, _) = await SetupAsync(billingBlocked: true);
        await using var _ = app;

        await ProcessAsync(app);

        Assert.Empty(model.Requests);
        Assert.Equal(AgentMessageStatus.Failed, (await InboundAsync(app)).Status);
    }

    [Fact]
    public async Task Non_admin_owner_is_not_processed()
    {
        var (app, model, telegram, _) = await SetupAsync();
        await using var _ = app;
        await app.SeedAsync(async db =>
        {
            var owner = await db.Users.SingleAsync(u => u.TelegramChatId == 777);
            owner.Role = UserRole.Recepcao;
            await db.SaveChangesAsync();
        });

        await ProcessAsync(app);

        Assert.Empty(model.Requests);
        Assert.Empty(telegram.Sent);
        Assert.Equal(AgentMessageStatus.Failed, (await InboundAsync(app)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Message_arriving_during_the_run_stays_pending_for_next_cycle(bool llmFails)
    {
        var (app, model, _, _) = await SetupAsync();
        await using var _ = app;
        if (llmFails) model.Throw();
        else model.Reply("Olá!");
        model.OnCall = () => app.SeedAsync(async db =>
        {
            var conversation = await db.AgentConversations.SingleAsync();
            db.AgentMessages.Add(new AgentMessage { ConversationId = conversation.Id, Channel = AgentChannel.Telegram,
                Role = AgentMessageRole.User, Content = "chegou agora", Status = AgentMessageStatus.Pending,
                ExternalMessageId = "777:2" });
            await db.SaveChangesAsync();
        });

        await ProcessAsync(app);

        var late = await InboundAsync(app, "777:2");
        Assert.Equal(AgentMessageStatus.Pending, late.Status);
        Assert.Equal(0, late.Attempts);
        var first = await InboundAsync(app);
        Assert.Equal(llmFails ? AgentMessageStatus.Pending : AgentMessageStatus.Done, first.Status);
        Assert.Equal(llmFails ? 1 : 0, first.Attempts);
    }

    [Fact]
    public async Task Broken_conversation_does_not_block_the_others()
    {
        var model = new FakeChatModel();
        var telegram = new FakeTelegramClient();
        var app = new MultiClinicaFactory
        {
            ConfigureTestServices = s =>
            {
                s.AddSingleton<IChatModel>(model);
                s.AddSingleton<ITelegramClient>(telegram);
                s.Configure<AgentOptions>(o => o.Model = "modelo-teste");
            },
        };
        await using var _ = app;
        var (clinicId, adminId) = await AgentSeed.ClinicWithAdminAsync(app, telegramChatId: 777);
        // Chat id não numérico estoura no long.Parse, fora do try. Entra primeiro para ser processado antes.
        await app.SeedAsync(async db =>
        {
            var broken = new AgentConversation { ClinicaId = clinicId, Channel = AgentChannel.Telegram,
                Profile = AgentProfile.Owner, ExternalChatId = "nao-numerico", UserId = adminId, LastInboundAt = DateTime.UtcNow };
            broken.Messages.Add(new AgentMessage { Channel = AgentChannel.Telegram, Role = AgentMessageRole.User,
                Content = "quebrada", Status = AgentMessageStatus.Pending, ExternalMessageId = "nao-numerico:1" });
            db.AgentConversations.Add(broken);
            await db.SaveChangesAsync();
        });
        await app.SeedAsync(async db =>
        {
            var good = new AgentConversation { ClinicaId = clinicId, Channel = AgentChannel.Telegram,
                Profile = AgentProfile.Owner, ExternalChatId = "777", UserId = adminId, LastInboundAt = DateTime.UtcNow };
            good.Messages.Add(new AgentMessage { Channel = AgentChannel.Telegram, Role = AgentMessageRole.User,
                Content = "oi", Status = AgentMessageStatus.Pending, ExternalMessageId = "777:1" });
            db.AgentConversations.Add(good);
            await db.SaveChangesAsync();
        });
        model.Reply("Olá!");

        await ProcessAsync(app);

        Assert.Equal([(777L, "Olá!")], telegram.Sent);
        Assert.Equal(AgentMessageStatus.Done, (await InboundAsync(app)).Status);
    }

    // Relógio manual: o backoff lê o TimeProvider, e o UpdatedAt gravado pelo banco usa o relógio real.
    public sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
