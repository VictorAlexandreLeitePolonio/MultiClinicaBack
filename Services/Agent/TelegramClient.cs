namespace MultiClinica.API.Services.Agent;

public interface ITelegramClient
{
    Task SendMessageAsync(long chatId, string text, CancellationToken ct);
}
