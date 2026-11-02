using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.Transcription;

// API compatível com OpenAI (/audio/transcriptions): faster-whisper local ou OpenAI.
public sealed class HttpTranscriber(HttpClient http, IOptions<TranscriptionOptions> options) : ITranscriber
{
    public async Task<TranscriptionResult> TranscribeAsync(Stream audio, string fileName, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            throw new TranscriptionNotConfiguredException();

        using var content = new MultipartFormDataContent();
        using var file = new StreamContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        content.Add(file, "file", fileName);
        content.Add(new StringContent(settings.Model), "model");
        content.Add(new StringContent("pt"), "language");
        content.Add(new StringContent("verbose_json"), "response_format");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/audio/transcriptions")
        {
            Content = content,
        };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Transcritor respondeu {(int)response.StatusCode} ({response.StatusCode}).",
                inner: null, statusCode: response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var text = json.RootElement.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;
        var seconds = json.RootElement.TryGetProperty("duration", out var durationElement)
            && durationElement.ValueKind == JsonValueKind.Number
                ? (int)Math.Ceiling(durationElement.GetDouble())
                : 0;

        return new TranscriptionResult(text, seconds);
    }
}
