namespace MultiClinica.API.Services.Agent;

public enum AgentChatRole { System, User, Assistant, Tool }

public sealed record AgentToolCall(string Id, string Name, string ArgumentsJson);

public sealed record AgentChatMessage(AgentChatRole Role, string? Content,
    IReadOnlyList<AgentToolCall>? ToolCalls = null, string? ToolCallId = null);

public sealed record AgentToolDefinition(string Name, string Description, string ParametersJsonSchema);

public sealed record AgentChatResult(string? Content, IReadOnlyList<AgentToolCall> ToolCalls,
    int InputTokens, int OutputTokens);

// Duas implementações: OpenAiChatModel (produção) e FakeChatModel (testes).
public interface IChatModel
{
    Task<AgentChatResult> CompleteAsync(string model, IReadOnlyList<AgentChatMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools, CancellationToken ct);
}
