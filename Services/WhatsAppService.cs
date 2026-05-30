using System.Text;
using System.Text.Json;

namespace ProjetoLP.API.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly string _instance;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppService> logger)
    {
        _logger     = logger;
        _httpClient = httpClient;
        _instance   = config["EvolutionApi:Instance"]!;
    }

    public async Task<bool> SendTextAsync(string phone, string message)
    {
        // Normaliza o número: remove tudo que não for dígito e garante o código do país (55).
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (!digits.StartsWith("55"))
            digits = "55" + digits;

        var payload = new
        {
            number      = digits,
            textMessage = new { text = message }
        };

        var json    = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url      = $"/message/sendText/{_instance}";
        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "[WhatsAppService] Falha ao enviar para {Phone}. Status: {Status}. Body: {Body}",
                phone, response.StatusCode, body);
            return false;
        }

        return true;
    }
}
