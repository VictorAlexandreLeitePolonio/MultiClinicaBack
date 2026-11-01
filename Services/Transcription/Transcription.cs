namespace MultiClinica.API.Services.Transcription;

public sealed class TranscriptionOptions
{
    // API compatível com OpenAI (/audio/transcriptions): faster-whisper local ou OpenAI.
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "whisper-1";
    public string StoragePath { get; set; } = "App_Data/consultations";
    public int PollSeconds { get; set; } = 10;
}

public sealed record TranscriptionResult(string Text, int DurationSeconds);

public sealed class TranscriptionNotConfiguredException() : Exception("Transcrição não configurada.");

// Duas implementações: HttpTranscriber (produção) e FakeTranscriber (testes).
public interface ITranscriber
{
    Task<TranscriptionResult> TranscribeAsync(Stream audio, string fileName, CancellationToken ct);
}
