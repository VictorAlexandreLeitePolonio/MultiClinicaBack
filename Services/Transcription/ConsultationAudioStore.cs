using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.Transcription;

// Pedaços do áudio em {StoragePath}/{reportId}/{n:D5}.webm, fora do wwwroot.
public sealed class ConsultationAudioStore(IOptions<TranscriptionOptions> options, IWebHostEnvironment env)
{
    private readonly string _root = Path.GetFullPath(options.Value.StoragePath, env.ContentRootPath);

    public string DirectoryFor(int reportId) => Path.Combine(_root, reportId.ToString());

    public async Task SaveChunkAsync(int reportId, int n, Stream body, CancellationToken ct)
    {
        var dir = DirectoryFor(reportId);
        Directory.CreateDirectory(dir);
        await using var file = File.Create(Path.Combine(dir, $"{n:D5}.webm"));
        await body.CopyToAsync(file, ct);
    }

    public IReadOnlyList<string> ListChunks(int reportId)
    {
        var dir = DirectoryFor(reportId);
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.webm").Order(StringComparer.Ordinal).ToList()
            : [];
    }

    // Só o 1º pedaço do MediaRecorder tem o cabeçalho WebM; juntar em ordem refaz o arquivo. Quem chama apaga o temporário.
    public async Task<string> ConcatToTempFileAsync(int reportId, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"consultation-{reportId}-{Guid.NewGuid():N}.webm");
        await using var output = File.Create(temp);
        foreach (var chunk in ListChunks(reportId))
        {
            await using var input = File.OpenRead(chunk);
            await input.CopyToAsync(output, ct);
        }
        return temp;
    }

    public void Delete(int reportId)
    {
        var dir = DirectoryFor(reportId);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
