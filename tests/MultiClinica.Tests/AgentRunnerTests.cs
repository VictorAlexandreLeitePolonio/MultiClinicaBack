using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class AgentRunnerTests
{
    private static Task<(MultiClinicaFactory App, FakeChatModel Model, int ConversationId)> SetupAsync(params string[] pending)
        => SetupAsync(historyHours: 24, pending);

    private static async Task<(MultiClinicaFactory App, FakeChatModel Model, int ConversationId)> SetupAsync(int historyHours,
        params string[] pending)
    {
        var model = new FakeChatModel();
        var app = new MultiClinicaFactory
        {
            ConfigureTestServices = s =>
            {
                s.AddSingleton<IChatModel>(model);
                s.Configure<AgentOptions>(o => { o.Model = "modelo-teste"; o.MaxToolIterations = 3; o.HistoryHours = historyHours; });
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

    private static async Task<string> RunAsync(MultiClinicaFactory app, int conversationId, int identityOffset = 0,
        int maxMessageId = int.MaxValue)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conversation = await db.AgentConversations.SingleAsync(c => c.Id == conversationId);
        var owner = await db.Users.SingleAsync(u => u.Id == conversation.UserId);
        scope.ServiceProvider.GetRequiredService<AgentIdentity>().Set(owner.ClinicaId, owner.Id + identityOffset, owner.Role);
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(conversation, owner, maxMessageId, CancellationToken.None);
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

    [Fact]
    public async Task Assistant_tool_call_without_result_is_left_out_of_the_history()
    {
        var (app, model, conversationId) = await SetupAsync();
        await using var _ = app;
        await app.SeedAsync(async db =>
        {
            var callsJson = JsonSerializer.Serialize(new[] { new AgentToolCall("x", "buscar_paciente", "{}") });
            db.AgentMessages.AddRange(
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "antiga", Status = AgentMessageStatus.Done },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.Assistant, ToolCallsJson = callsJson },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "nova", Status = AgentMessageStatus.Pending });
            await db.SaveChangesAsync();
        });
        model.Reply("ok");

        await RunAsync(app, conversationId);

        var messages = model.Requests[0].Messages;
        Assert.DoesNotContain(messages, m => m.ToolCalls is { Count: > 0 } calls && calls.Any(c => c.Id == "x"));
        Assert.Equal(["antiga", "nova"], messages.Where(m => m.Role == AgentChatRole.User).Select(m => m.Content));
    }

    [Fact]
    public async Task Tool_row_without_an_assistant_is_dropped_from_the_history()
    {
        var (app, model, conversationId) = await SetupAsync();
        await using var _ = app;
        await app.SeedAsync(async db =>
        {
            db.AgentMessages.AddRange(
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "antiga", Status = AgentMessageStatus.Done },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.Tool, Content = "{}", ToolCallId = "z" },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "nova", Status = AgentMessageStatus.Pending });
            await db.SaveChangesAsync();
        });
        model.Reply("ok");

        await RunAsync(app, conversationId);

        Assert.DoesNotContain(model.Requests[0].Messages, m => m.Role == AgentChatRole.Tool);
    }

    [Fact]
    public async Task Assistant_with_one_missing_result_is_dropped_together_with_the_present_one()
    {
        var (app, model, conversationId) = await SetupAsync();
        await using var _ = app;
        await app.SeedAsync(async db =>
        {
            var callsJson = JsonSerializer.Serialize(new[]
            {
                new AgentToolCall("x", "buscar_paciente", "{}"),
                new AgentToolCall("y", "buscar_paciente", "{}"),
            });
            db.AgentMessages.AddRange(
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "antiga", Status = AgentMessageStatus.Done },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.Assistant, ToolCallsJson = callsJson },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.Tool, Content = "{}", ToolCallId = "x" },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "nova", Status = AgentMessageStatus.Pending });
            await db.SaveChangesAsync();
        });
        model.Reply("ok");

        await RunAsync(app, conversationId);

        var roles = model.Requests[0].Messages.Skip(1).Select(m => m.Role);
        Assert.Equal([AgentChatRole.User, AgentChatRole.User], roles);
    }

    [Fact]
    public async Task Tool_result_is_placed_right_after_its_assistant_past_interleaved_user_rows()
    {
        var (app, model, conversationId) = await SetupAsync();
        await using var _ = app;
        await app.SeedAsync(async db =>
        {
            var callsJson = JsonSerializer.Serialize(new[] { new AgentToolCall("x", "buscar_paciente", "{}") });
            db.AgentMessages.AddRange(
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "antiga", Status = AgentMessageStatus.Done },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.Assistant, ToolCallsJson = callsJson },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "no meio", Status = AgentMessageStatus.Done },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.Tool, Content = "{}", ToolCallId = "x" },
                new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram, Role = AgentMessageRole.User, Content = "nova", Status = AgentMessageStatus.Pending });
            await db.SaveChangesAsync();
        });
        model.Reply("ok");

        await RunAsync(app, conversationId);

        var messages = model.Requests[0].Messages.Skip(1).ToList();
        Assert.Equal([AgentChatRole.User, AgentChatRole.Assistant, AgentChatRole.Tool, AgentChatRole.User, AgentChatRole.User],
            messages.Select(m => m.Role));
        Assert.Equal("x", messages[2].ToolCallId);
        Assert.Equal(["antiga", "no meio", "nova"], messages.Where(m => m.Role == AgentChatRole.User).Select(m => m.Content));
    }

    [Fact]
    public async Task Rows_after_the_batch_bound_stay_out_of_the_request()
    {
        var (app, model, conversationId) = await SetupAsync("oi");
        await using var _ = app;
        var batchMax = 0;
        await app.SeedAsync(async db => batchMax = await db.AgentMessages.Where(m => m.ConversationId == conversationId).MaxAsync(m => m.Id));
        await app.SeedAsync(async db =>
        {
            db.AgentMessages.Add(new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram,
                Role = AgentMessageRole.User, Content = "chegou depois", Status = AgentMessageStatus.Pending });
            await db.SaveChangesAsync();
        });
        model.Reply("ok");

        await RunAsync(app, conversationId, maxMessageId: batchMax);

        Assert.Equal(["oi"], model.Requests[0].Messages.Where(m => m.Role == AgentChatRole.User).Select(m => m.Content));
        await app.SeedAsync(async db =>
            Assert.Equal(AgentMessageStatus.Pending, (await db.AgentMessages.SingleAsync(m => m.Content == "chegou depois")).Status));
    }

    [Fact]
    public async Task Pending_turn_is_sent_even_when_it_is_outside_the_history_window()
    {
        // HistoryHours 0: a janela de tempo não traz nada; só a regra de Pending pode trazer o turno.
        var (app, model, conversationId) = await SetupAsync(historyHours: 0, "oi, de antes da janela");
        await using var _ = app;
        await app.SeedAsync(async db =>
        {
            db.AgentMessages.Add(new AgentMessage { ConversationId = conversationId, Channel = AgentChannel.Telegram,
                Role = AgentMessageRole.User, Content = "antiga", Status = AgentMessageStatus.Done });
            await db.SaveChangesAsync();
        });
        model.Reply("ok");

        await RunAsync(app, conversationId);

        Assert.Equal(["oi, de antes da janela"], model.Requests[0].Messages.Where(m => m.Role == AgentChatRole.User).Select(m => m.Content));
    }
}
