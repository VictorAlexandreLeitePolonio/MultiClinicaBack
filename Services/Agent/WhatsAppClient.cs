using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.Agent;

public sealed class WhatsAppOptions
{
    public string GraphBaseUrl { get; set; } = "https://graph.facebook.com/v23.0/";
    public string AccessToken { get; set; } = string.Empty;
    // Número da plataforma (copiloto do dono). Webhooks de outros números são ignorados.
    public string PhoneNumberId { get; set; } = string.Empty;
    // E.164 sem "+", usado no link wa.me.
    public string DisplayNumber { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
}

public interface IWhatsAppClient
{
    Task SendTextAsync(string to, string text, CancellationToken ct);
}

public sealed class WhatsAppClient(HttpClient http, IOptions<WhatsAppOptions> options) : IWhatsAppClient
{
    public async Task SendTextAsync(string to, string text, CancellationToken ct)
    {
        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{o.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(new { messaging_product = "whatsapp", to, type = "text", text = new { body = text } }),
        };
        request.Headers.Authorization = new("Bearer", o.AccessToken);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"WhatsApp respondeu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}",
                null, response.StatusCode);
    }
}
