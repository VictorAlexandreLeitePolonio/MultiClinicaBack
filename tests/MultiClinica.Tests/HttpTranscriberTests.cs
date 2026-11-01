using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MultiClinica.API.Services.Transcription;
using Xunit;

namespace MultiClinica.Tests;

public class HttpTranscriberTests
{
    private sealed record Part(string Name, string? FileName, string? ContentType, byte[] Bytes);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Authorization { get; private set; }
        public List<Part> Parts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Authorization = request.Headers.Authorization?.ToString();
            if (request.Content is MultipartFormDataContent multipart)
            {
                foreach (var content in multipart)
                {
                    var disposition = content.Headers.ContentDisposition!;
                    Parts.Add(new Part(
                        disposition.Name!.Trim('"'),
                        disposition.FileName?.Trim('"'),
                        content.Headers.ContentType?.MediaType,
                        await content.ReadAsByteArrayAsync(cancellationToken)));
                }
            }
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private static HttpTranscriber Build(FakeHandler handler, string? baseUrl = "https://whisper.test/v1",
        string? apiKey = "chave", string model = "whisper-1")
        => new(new HttpClient(handler), Options.Create(new TranscriptionOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Model = model,
        }));

    private static string TextOf(Part part) => Encoding.UTF8.GetString(part.Bytes);

    [Fact]
    public async Task Envia_multipart_com_arquivo_modelo_idioma_e_formato()
    {
        var handler = new FakeHandler(_ => Json("""{"text":"dor no joelho","duration":61.2}"""));
        var transcriber = Build(handler);

        var result = await transcriber.TranscribeAsync(new MemoryStream("audio"u8.ToArray()), "audio.webm", default);

        Assert.Equal("POST", handler.Request!.Method.Method);
        Assert.Equal("https://whisper.test/v1/audio/transcriptions", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer chave", handler.Authorization);
        Assert.Equal(4, handler.Parts.Count);

        var file = Assert.Single(handler.Parts, part => part.Name == "file");
        Assert.Equal("audio.webm", file.FileName);
        Assert.Equal("audio/webm", file.ContentType);
        Assert.Equal("audio", TextOf(file));
        Assert.Equal("whisper-1", TextOf(Assert.Single(handler.Parts, part => part.Name == "model")));
        Assert.Equal("pt", TextOf(Assert.Single(handler.Parts, part => part.Name == "language")));
        Assert.Equal("verbose_json", TextOf(Assert.Single(handler.Parts, part => part.Name == "response_format")));

        Assert.Equal("dor no joelho", result.Text);
        Assert.Equal(62, result.DurationSeconds);
    }

    [Fact]
    public async Task Sem_chave_nao_envia_authorization()
    {
        var handler = new FakeHandler(_ => Json("""{"text":"ok","duration":1}"""));
        var transcriber = Build(handler, apiKey: null);

        await transcriber.TranscribeAsync(new MemoryStream("audio"u8.ToArray()), "audio.webm", default);

        Assert.Null(handler.Authorization);
    }

    [Fact]
    public async Task Sem_BaseUrl_lanca_TranscriptionNotConfiguredException()
    {
        var handler = new FakeHandler(_ => Json("{}"));
        var transcriber = Build(handler, baseUrl: null);

        await Assert.ThrowsAsync<TranscriptionNotConfiguredException>(
            () => transcriber.TranscribeAsync(new MemoryStream("audio"u8.ToArray()), "audio.webm", default));
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Resposta_nao_2xx_lanca_HttpRequestException_com_o_status()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var transcriber = Build(handler);

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => transcriber.TranscribeAsync(new MemoryStream("audio"u8.ToArray()), "audio.webm", default));

        Assert.Equal(HttpStatusCode.BadGateway, error.StatusCode);
    }
}
