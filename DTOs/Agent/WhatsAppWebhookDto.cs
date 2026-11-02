using System.Text.Json.Serialization;

namespace MultiClinica.API.DTOs.Agent;

public sealed class WhatsAppWebhookDto
{
    [JsonPropertyName("entry")] public List<WhatsAppEntryDto> Entry { get; set; } = [];
}

public sealed class WhatsAppEntryDto
{
    [JsonPropertyName("changes")] public List<WhatsAppChangeDto> Changes { get; set; } = [];
}

public sealed class WhatsAppChangeDto
{
    [JsonPropertyName("value")] public WhatsAppValueDto? Value { get; set; }
}

public sealed class WhatsAppValueDto
{
    [JsonPropertyName("metadata")] public WhatsAppMetadataDto? Metadata { get; set; }
    [JsonPropertyName("messages")] public List<WhatsAppMessageDto>? Messages { get; set; }
    [JsonPropertyName("statuses")] public List<WhatsAppStatusDto>? Statuses { get; set; }
}

public sealed class WhatsAppStatusDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("errors")] public List<WhatsAppStatusErrorDto>? Errors { get; set; }
}

public sealed class WhatsAppStatusErrorDto
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
}

public sealed class WhatsAppMetadataDto
{
    [JsonPropertyName("phone_number_id")] public string PhoneNumberId { get; set; } = string.Empty;
}

public sealed class WhatsAppMessageDto
{
    [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("text")] public WhatsAppTextDto? Text { get; set; }
}

public sealed class WhatsAppTextDto
{
    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
}
