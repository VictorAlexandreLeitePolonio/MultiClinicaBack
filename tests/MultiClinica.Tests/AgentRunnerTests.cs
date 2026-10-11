using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class AgentRunnerTests
{
    private static async Task<(MultiClinicaFactory App, FakeChatModel Model, int ConversationId)> SetupAsync(params string[] pending)
    {
        var model = new FakeChatModel();
        var app = new MultiClinicaFactory
        {
            ConfigureTestServices = s =>
            {
                s.AddSingleton<IChatModel>(model);
                s.Configure<AgentOptions>(o => { o.Model = "modelo-teste"; o.MaxToolIterations = 3; });
            },
        };
        var (clinicId, adminId) = await AgentSeed.ClinicWithAdminAsync(app, telegramChatId: 555);
        var conversationId = 0;
        await app.SeedAsync(async db =>
        {
            db.Patients.Add(new Patient { ClinicaId = clinicId, Name = "Maria Aparecida" });
            var conversation = new AgentConversation { ClinicaId = clinicId, Channel = AgentChannel.Telegram,
                Profile = AgentProfile.Owner, ExternalChatId = "555", UserId = adminId, LastInboundAt = DateTime.UtcNow };
            foreach (var text in pending)
                conversation.Messages.Add(new AgentMessage { Channel = AgentChannel.Telegram, Role = AgentMessageRole.User,
                    Content = text, Status = AgentMessageStatus.Pending });
            db.AgentConversations.Add(conversation);
            await db.SaveChangesAsync();
            conversationId = conversation.Id;
        });
        return (app, model, conversationId);
    }

    private static async Task<string> RunAsync(MultiClinicaFactory app, int conversationId, int identityOffset = 0)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conversation = await db.AgentConversations.SingleAsync(c => c.Id == conversationId);
        var owner = await db.Users.SingleAsync(u => u.Id == conversation.UserId);
        scope.ServiceProvider.GetRequiredService<AgentIdentity>().Set(owner.ClinicaId, owner.Id + identityOffset, owner.Role);
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(conversation, owner, CancellationToken.None);
    }

    [Fact]
    public async Task Executes_tools_and_persists_the_audit_trail()
    {
        var (app, model, conversationId) = await SetupAsync("oi", "acha a cida");
        await using var _ = app;
        model.CallTool("buscar_paciente", """{"nome":"cida"}""").Reply("Achei a Maria Aparecida.");

        var reply = await RunAsync(app, conversationId);

        Assert.Equal("Achei a Maria Aparecida.", reply);
        // As duas mensagens pendentes chegam juntas no primeiro request.
        var first = model.Requests[0].Messages;
        Assert.Equal(["oi", "acha a cida"], first.Where(m => m.Role == AgentChatRole.User).Select(m => m.Content));
        // O resultado da tool volta para o modelo no segundo request.
        var toolResult = model.Requests[1].Messages.Last();
        Assert.Equal(AgentChatRole.Tool, toolResult.Role);
        Assert.Contains("Maria Aparecida", toolResult.Content);

        await app.SeedAsync(async db =>
        {
            var roles = await db.AgentMessages.Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Id).Select(m => m.Role).ToListAsync();
            Assert.Equal([AgentMessageRole.User, AgentMessageRole.User, AgentMessageRole.Assistant,
                AgentMessageRole.Tool, AgentMessageRole.Assistant], roles);
            var last = await db.AgentMessages.OrderBy(m => m.Id).LastAsync();
            Assert.Equal("modelo-teste", last.Model);
            Assert.Equal(10, last.InputTokens);
        });
    }

    [Fact]
    public async Task System_prompt_carries_today_and_professionals()
    {
        var (app, model, conversationId) = await SetupAsync("o que tenho amanhã?");
        await using var _ = app;
        model.Reply("Nada.");

        await RunAsync(app, conversationId);

        var system = model.Requests[0].Messages[0];
        Assert.Equal(AgentChatRole.System, system.Role);
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AgentTime.FindZone("America/Sao_Paulo"));
        Assert.Contains(localToday.ToString("yyyy-MM-dd"), system.Content);
        Assert.Contains("Victor", system.Content);
    }

    [Fact]
    public async Task Stops_after_max_iterations()
    {
        var (app, model, conversationId) = await SetupAsync("loop");
        await using var _ = app;
        for (var i = 0; i < 3; i++) model.CallTool("buscar_paciente", """{"nome":"x"}""", $"call_{i}");

        await Assert.ThrowsAsync<AgentLoopLimitException>(() => RunAsync(app, conversationId));

        // A 3ª volta não executa tool nem grava Assistant com tool_calls sem resultado.
        await app.SeedAsync(async db =>
        {
            var rows = await db.AgentMessages.Where(m => m.ConversationId == conversationId)
                .Select(m => m.Role).ToListAsync();
            Assert.Equal(2, rows.Count(r => r == AgentMessageRole.Tool));
            Assert.Equal(2, rows.Count(r => r == AgentMessageRole.Assistant));
        });
    }

    [Fact]
    public async Task Refuses_to_run_when_agent_identity_is_another_user()
    {
        var (app, model, conversationId) = await SetupAsync("oi");
        await using var _ = app;
        model.Reply("nunca");

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(app, conversationId, identityOffset: 999));

        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Second_run_replays_the_earlier_tool_call_from_the_database()
    {
        var (app, model, conversationId) = await SetupAsync("acha a cida");
        await using var _ = app;
        model.CallTool("buscar_paciente", """{"nome":"cida"}""", "call_1").Reply("Achei.");
        await RunAsync(app, conversationId);

        await app.SeedAsync(async db =>
        {
            db.AgentMessages.Add(new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram,
                Role = AgentMessageRole.User, Content = "e a próxima?", Status = AgentMessageStatus.Pending });
            await db.SaveChangesAsync();
        });
        model.Reply("Ok.");
        await RunAsync(app, conversationId);

        var messages = model.Requests[1].Messages;
        var assistantIndex = messages.FindIndex(m => m.Role == AgentChatRole.Assistant && m.ToolCalls is { Count: > 0 });
        Assert.True(assistantIndex >= 0);
        var call = Assert.Single(messages[assistantIndex].ToolCalls!);
        Assert.Equal("call_1", call.Id);
        Assert.Equal("buscar_paciente", call.Name);
        Assert.Equal(AgentChatRole.Tool, messages[assistantIndex + 1].Role);
        Assert.Equal("call_1", messages[assistantIndex + 1].ToolCallId);
    }

    [Fact]
    public async Task History_never_starts_with_an_orphan_tool_message()
    {
        var (app, model, conversationId) = await SetupAsync();
        await using var _ = app;
        await app.SeedAsync(async db =>
        {
            db.AgentMessages.AddRange(
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.Tool, Content = "{}", ToolCallId = "velho" },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "nova", Status = AgentMessageStatus.Pending });
            await db.SaveChangesAsync();
        });
        model.Reply("ok");

        await RunAsync(app, conversationId);

        Assert.Equal(AgentChatRole.User, model.Requests[0].Messages[1].Role);
    }
}
