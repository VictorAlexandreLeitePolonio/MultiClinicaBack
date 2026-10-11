using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace MultiClinica.API.Services.Agent;

// Fala com qualquer endpoint compatível com OpenAI /chat/completions (OpenCode Go por padrão).
public sealed class OpenAiChatModel(IOptions<AgentOptions> options) : IChatModel
{
    public async Task<AgentChatResult> CompleteAsync(string model, IReadOnlyList<AgentChatMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools, CancellationToken ct)
    {
        var o = options.Value;
        var client = new ChatClient(model, new ApiKeyCredential(o.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(o.BaseUrl), NetworkTimeout = TimeSpan.FromSeconds(90) });

        var chatOptions = new ChatCompletionOptions();
        foreach (var tool in tools)
            chatOptions.Tools.Add(ChatTool.CreateFunctionTool(tool.Name, tool.Description,
                BinaryData.FromString(tool.ParametersJsonSchema)));

        ChatCompletion completion = await client.CompleteChatAsync(messages.Select(ToSdk), chatOptions, ct);

        var text = string.Concat(completion.Content
            .Where(p => p.Kind == ChatMessageContentPartKind.Text)
            .Select(p => p.Text));
        var calls = completion.ToolCalls
            .Select(c => new AgentToolCall(c.Id, c.FunctionName, c.FunctionArguments.ToString()))
            .ToList();
        return new AgentChatResult(string.IsNullOrWhiteSpace(text) ? null : text, calls,
            completion.Usage?.InputTokenCount ?? 0, completion.Usage?.OutputTokenCount ?? 0);
    }

    private static ChatMessage ToSdk(AgentChatMessage m) => m.Role switch
    {
        AgentChatRole.System => new SystemChatMessage(m.Content ?? string.Empty),
        AgentChatRole.User => new UserChatMessage(m.Content ?? string.Empty),
        AgentChatRole.Tool => new ToolChatMessage(m.ToolCallId!, m.Content ?? string.Empty),
        _ when m.ToolCalls is { Count: > 0 } => new AssistantChatMessage(m.ToolCalls
            .Select(c => ChatToolCall.CreateFunctionToolCall(c.Id, c.Name, BinaryData.FromString(c.ArgumentsJson)))
            .ToList()),
        _ => new AssistantChatMessage(m.Content ?? string.Empty),
    };
}
