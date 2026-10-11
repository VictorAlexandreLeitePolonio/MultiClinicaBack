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
    IUsuarioLogadoService usuario,
    IOptions<AgentOptions> options,
    TimeProvider time)
{
    // ponytail: só o perfil Owner por enquanto; o perfil Patient entra no plano do WhatsApp.
    public async Task<string> RunAsync(AgentConversation conversation, User owner, CancellationToken ct)
    {
        if (usuario.ClinicaId != conversation.ClinicaId || usuario.UserId != owner.Id)
            throw new InvalidOperationException("Identidade do agente não corresponde à conversa.");

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

            // Última volta: sem próxima chamada para devolver o resultado, então não executa tool nem grava linha órfã.
            if (result.ToolCalls.Count > 0 && i == o.MaxToolIterations - 1)
                throw new AgentLoopLimitException();

            // Grava o Assistant antes de executar as tools: as tools commitam pelos próprios services.
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
            await db.SaveChangesAsync(ct);
            messages.Add(new AgentChatMessage(AgentChatRole.Assistant, result.Content, result.ToolCalls));

            if (result.ToolCalls.Count == 0)
                return result.Content ?? "Pronto.";

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
                await db.SaveChangesAsync(ct);
                messages.Add(new AgentChatMessage(AgentChatRole.Tool, output, ToolCallId: call.Id));
            }
        }

        throw new AgentLoopLimitException();
    }

    private async Task<List<AgentChatMessage>> LoadHistoryAsync(int conversationId, DateTime sinceUtc, int max,
        CancellationToken ct)
    {
        // ponytail: janela por número de linhas (HistoryMaxMessages), não por tokens; trocar por orçamento de tokens se o prompt crescer demais.
        var rows = await db.AgentMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.CreatedAt >= sinceUtc
                && m.Status != AgentMessageStatus.Failed)
            .OrderByDescending(m => m.Id).Take(max)
            .ToListAsync(ct);
        rows.Reverse();
        // O corte por quantidade pode deixar um tool/assistant órfão no início; a API rejeita isso.
        var firstUser = rows.FindIndex(m => m.Role == AgentMessageRole.User);
        if (firstUser < 0) return [];

        return DropUnpairedToolRows(rows.Skip(firstUser).ToList()).Select(m => m.Role switch
        {
            AgentMessageRole.User => new AgentChatMessage(AgentChatRole.User, m.Content),
            AgentMessageRole.Tool => new AgentChatMessage(AgentChatRole.Tool, m.Content, ToolCallId: m.ToolCallId),
            _ => new AgentChatMessage(AgentChatRole.Assistant, m.Content,
                m.ToolCallsJson is null ? null : JsonSerializer.Deserialize<List<AgentToolCall>>(m.ToolCallsJson)),
        }).ToList();
    }

    // Assistant com tool_calls só fica se TODAS as chamadas tiverem Tool logo depois; Tool só fica se responde
    // a uma chamada de um Assistant mantido. Turno interrompido (crash entre o Assistant e a tool) deixa isso.
    private static List<AgentMessage> DropUnpairedToolRows(List<AgentMessage> rows)
    {
        var kept = new List<AgentMessage>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            // Tool entra junto com o Assistant que o chamou; um Tool visto aqui sozinho é órfão.
            if (row.Role == AgentMessageRole.Tool) continue;
            if (row.ToolCallsJson is null)
            {
                kept.Add(row);
                continue;
            }

            var calls = JsonSerializer.Deserialize<List<AgentToolCall>>(row.ToolCallsJson) ?? [];
            var results = new List<AgentMessage>();
            for (var j = i + 1; j < rows.Count && rows[j].Role == AgentMessageRole.Tool; j++) results.Add(rows[j]);
            if (!calls.All(c => results.Any(r => r.ToolCallId == c.Id))) continue;

            kept.Add(row);
            kept.AddRange(results.Where(r => calls.Any(c => c.Id == r.ToolCallId)));
        }
        return kept;
    }
}
