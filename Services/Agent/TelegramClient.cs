using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.Agent;

public sealed class TelegramOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string BotUsername { get; set; } = "MultiClinicaBot";
}

public interface ITelegramClient
{
    Task SendMessageAsync(long chatId, string text, CancellationToken ct);
}

public sealed class TelegramClient(HttpClient http, IOptions<TelegramOptions> options) : ITelegramClient
{
    public async Task SendMessageAsync(long chatId, string text, CancellationToken ct)
    {
        // O token vai na URL: System.Net.Http.HttpClient precisa ficar em Warning (appsettings.json) para não logar o path.
        using var response = await http.PostAsJsonAsync($"bot{options.Value.BotToken}/sendMessage",
            new { chat_id = chatId, text }, ct);
        response.EnsureSuccessStatusCode();
    }
}
