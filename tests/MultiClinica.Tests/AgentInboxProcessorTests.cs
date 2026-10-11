using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class AgentInboxProcessorTests
{
    private static async Task<(MultiClinicaFactory App, FakeChatModel Model, FakeTelegramClient Telegram)> SetupAsync(
        bool billingBlocked = false)
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
        return (app, model, telegram);
    }

    private static Task ProcessAsync(MultiClinicaFactory app)
        => app.Services.GetRequiredService<AgentInboxProcessor>().ProcessBatchAsync(CancellationToken.None);

    private static async Task<AgentMessage> InboundAsync(MultiClinicaFactory app)
    {
        AgentMessage? message = null;
        await app.SeedAsync(async db => message = await db.AgentMessages.SingleAsync(m => m.ExternalMessageId == "777:1"));
        return message!;
    }

    [Fact]
    public async Task Replies_on_telegram_and_marks_done()
    {
        var (app, model, telegram) = await SetupAsync();
        await using var _ = app;
        model.Reply("Olá, Victor!");

        await ProcessAsync(app);

        Assert.Equal([(777L, "Olá, Victor!")], telegram.Sent);
        Assert.Equal(AgentMessageStatus.Done, (await InboundAsync(app)).Status);
    }

    [Fact]
    public async Task Retries_then_gives_up_with_apology()
    {
        var (app, model, telegram) = await SetupAsync();
        await using var _ = app;
        model.Throw().Throw().Throw();

        await ProcessAsync(app);
        var afterFirst = await InboundAsync(app);
        Assert.Equal(AgentMessageStatus.Pending, afterFirst.Status);
        Assert.Equal(1, afterFirst.Attempts);
        Assert.Empty(telegram.Sent);

        await ProcessAsync(app);
        await ProcessAsync(app);

        Assert.Equal(AgentMessageStatus.Failed, (await InboundAsync(app)).Status);
        Assert.Equal([(777L, AgentInboxProcessor.FailureReply)], telegram.Sent);
    }

    [Fact]
    public async Task Blocked_clinic_is_not_processed()
    {
        var (app, model, telegram) = await SetupAsync(billingBlocked: true);
        await using var _ = app;

        await ProcessAsync(app);

        Assert.Empty(model.Requests);
        Assert.Equal(AgentMessageStatus.Failed, (await InboundAsync(app)).Status);
    }
}
