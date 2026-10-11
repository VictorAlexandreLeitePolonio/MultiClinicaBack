namespace MultiClinica.API.Models;

public enum AgentMessageRole { User, Assistant, Tool }
public enum AgentMessageStatus { Pending, Done, Failed }

// Inbox + histórico + auditoria. Status só é preenchido em mensagens User.
public class AgentMessage : AuditableEntity
{
    public int ConversationId { get; set; }
    public AgentConversation Conversation { get; set; } = null!;
    public AgentChannel Channel { get; set; }
    public AgentMessageRole Role { get; set; }
    public string? Content { get; set; }
    public string? ToolCallsJson { get; set; }
    public string? ToolCallId { get; set; }
    // Id da mensagem de ENTRADA do webhook (ex.: "chatId:messageId" no Telegram); mensagens de saída não usam este campo.
    public string? ExternalMessageId { get; set; }
    public AgentMessageStatus? Status { get; set; }
    public int Attempts { get; set; }
    public string? Model { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? LatencyMs { get; set; }
}
