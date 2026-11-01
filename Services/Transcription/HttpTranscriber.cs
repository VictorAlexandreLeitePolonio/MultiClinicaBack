using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.Transcription;

// Stub da Fundação; o corpo é da Trilha B.
public sealed class HttpTranscriber(HttpClient http, IOptions<TranscriptionOptions> options) : ITranscriber
{
    public Task<TranscriptionResult> TranscribeAsync(Stream audio, string fileName, CancellationToken ct)
        => throw new TranscriptionNotConfiguredException();
}
