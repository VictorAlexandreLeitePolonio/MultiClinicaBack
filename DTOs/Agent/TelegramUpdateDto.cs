using System.Text.Json.Serialization;

namespace MultiClinica.API.DTOs.Agent;

public sealed class TelegramUpdateDto
{
    [JsonPropertyName("update_id")] public long UpdateId { get; set; }
    [JsonPropertyName("message")] public TelegramMessageDto? Message { get; set; }
}

public sealed class TelegramMessageDto
{
    [JsonPropertyName("message_id")] public long MessageId { get; set; }
    [JsonPropertyName("chat")] public TelegramChatDto Chat { get; set; } = new();
    [JsonPropertyName("text")] public string? Text { get; set; }
}

public sealed class TelegramChatDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
}
