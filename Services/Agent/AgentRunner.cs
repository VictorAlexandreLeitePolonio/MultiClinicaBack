using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services.Agent;

public sealed class AgentLoopLimitException() : Exception("O agente excedeu o limite de chamadas de ferramentas.");

public sealed class AgentRunner(
    AppDbContext db,
    IChatModel chat,
    OwnerAgentTools ownerTools,
    IAppointmentService appointments,
    IOptions<AgentOptions> options,
    TimeProvider time)
{
    // ponytail: só o perfil Owner por enquanto; o perfil Patient entra no plano do WhatsApp.
    public async Task<string> RunAsync(AgentConversation conversation, User owner, CancellationToken ct)
    {
        var o = options.Value;
        var model = o.ModelFor(conversation.Profile);
        var now = time.GetUtcNow().UtcDateTime;
        var clinic = await db.Clinicas.AsNoTracking().FirstAsync(c => c.Id == conversation.ClinicaId, ct);
        var tz = AgentTime.FindZone(clinic.TimeZoneId);
        var professionals = (await appointments.GetProfessionalsAsync()).Value!.Select(p => (p.Id, p.Name));

        var messages = new List<AgentChatMessage>
        {
            new(AgentChatRole.System, AgentPrompts.Owner(clinic.DisplayName ?? clinic.NomeFantasia, owner.Name, now, tz, professionals)),
        };
        messages.AddRange(await LoadHistoryAsync(conversation.Id, now.AddHours(-o.HistoryHours), o.HistoryMaxMessages, ct));

        for (var i = 0; i < o.MaxToolIterations; i++)
        {
            var watch = Stopwatch.StartNew();
            var result = await chat.CompleteAsync(model, messages, OwnerAgentTools.Definitions, ct);
            watch.Stop();

            db.AgentMessages.Add(new AgentMessage
            {
                ConversationId = conversation.Id,
                Channel = conversation.Channel,
                Role = AgentMessageRole.Assistant,
                Content = result.Content,
                ToolCallsJson = result.ToolCalls.Count > 0 ? JsonSerializer.Serialize(result.ToolCalls) : null,
                Model = model,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                LatencyMs = (int)watch.ElapsedMilliseconds,
            });
            messages.Add(new AgentChatMessage(AgentChatRole.Assistant, result.Content, result.ToolCalls));

            if (result.ToolCalls.Count == 0)
            {
                await db.SaveChangesAsync(ct);
                return result.Content ?? "Pronto.";
            }

            foreach (var call in result.ToolCalls)
            {
                var output = await ownerTools.ExecuteAsync(call.Name, call.ArgumentsJson);
                db.AgentMessages.Add(new AgentMessage
                {
                    ConversationId = conversation.Id,
                    Channel = conversation.Channel,
                    Role = AgentMessageRole.Tool,
                    Content = output,
                    ToolCallId = call.Id,
                });
                messages.Add(new AgentChatMessage(AgentChatRole.Tool, output, ToolCallId: call.Id));
            }
            await db.SaveChangesAsync(ct);
        }

        throw new AgentLoopLimitException();
    }

    private async Task<List<AgentChatMessage>> LoadHistoryAsync(int conversationId, DateTime sinceUtc, int max,
        CancellationToken ct)
    {
        var rows = await db.AgentMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.CreatedAt >= sinceUtc
                && m.Status != AgentMessageStatus.Failed)
            .OrderByDescending(m => m.Id).Take(max)
            .ToListAsync(ct);
        rows.Reverse();
        // O corte por quantidade pode deixar um tool/assistant órfão no início; a API rejeita isso.
        var firstUser = rows.FindIndex(m => m.Role == AgentMessageRole.User);
        if (firstUser < 0) return [];

        return rows.Skip(firstUser).Select(m => m.Role switch
        {
            AgentMessageRole.User => new AgentChatMessage(AgentChatRole.User, m.Content),
            AgentMessageRole.Tool => new AgentChatMessage(AgentChatRole.Tool, m.Content, ToolCallId: m.ToolCallId),
            _ => new AgentChatMessage(AgentChatRole.Assistant, m.Content,
                m.ToolCallsJson is null ? null : JsonSerializer.Deserialize<List<AgentToolCall>>(m.ToolCallsJson)),
        }).ToList();
    }
}
