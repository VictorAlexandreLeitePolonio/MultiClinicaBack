using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MultiClinica.API.Services;
using Xunit;

namespace MultiClinica.Tests;

public class ResendEmailSenderTests
{
    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request;
        public string? RequestBody;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static (ResendEmailSender Sender, FakeHandler Handler) Build(HttpStatusCode status, string body = "{}")
    {
        var handler = new FakeHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var options = new SmtpOptions { From = "onboarding@resend.dev", FromName = "MultiClínica" };
        return (new ResendEmailSender(http, options, NullLogger<ResendEmailSender>.Instance), handler);
    }

    [Fact]
    public async Task SendAsync_MontaPayloadCorreto()
    {
        var (sender, handler) = Build(HttpStatusCode.OK);

        await sender.SendAsync("paciente@teste.com", "Assunto", "<p>corpo</p>");

        Assert.Equal("https://api.resend.com/emails", handler.Request!.RequestUri!.ToString());
        var payload = JsonDocument.Parse(handler.RequestBody!).RootElement;
        Assert.Equal("MultiClínica <onboarding@resend.dev>", payload.GetProperty("from").GetString());
        Assert.Equal("paciente@teste.com", payload.GetProperty("to")[0].GetString());
        Assert.Equal("Assunto", payload.GetProperty("subject").GetString());
        Assert.Equal("<p>corpo</p>", payload.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendAsync_ErroDaApi_LancaExcecaoComDetalhe()
    {
        var (sender, _) = Build(HttpStatusCode.UnprocessableEntity, """{"message":"from inválido"}""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync("paciente@teste.com", "Assunto", "corpo"));

        Assert.Contains("422", ex.Message);
        Assert.Contains("from inválido", ex.Message);
    }
}
