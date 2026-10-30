using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class WhatsAppClientTests
{
    private const string PhoneNumberId = "PNID";
    private const string AccessToken = "tok";

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Method, string Url, string? Authorization, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), body));
            return respond(request);
        }
    }

    private static (WhatsAppClient Client, FakeHandler Handler) Build(FakeHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.test/v23.0/") };
        var options = Options.Create(new WhatsAppOptions
        {
            GraphBaseUrl = "https://graph.test/v23.0/",
            AccessToken = AccessToken,
            PhoneNumberId = PhoneNumberId,
        });
        return (new WhatsAppClient(http, options), handler);
    }

    [Fact]
    public async Task Sends_text_to_phone_number_endpoint_with_bearer_token()
    {
        var (client, handler) = Build(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"messages":[{"id":"wamid.1"}]}""", Encoding.UTF8, "application/json"),
        }));

        await client.SendTextAsync("5511999998888", "Olá!", CancellationToken.None);

        var call = Assert.Single(handler.Calls);
        Assert.Equal("POST", call.Method);
        Assert.Equal("https://graph.test/v23.0/PNID/messages", call.Url);
        Assert.Equal("Bearer tok", call.Authorization);
        var body = JsonDocument.Parse(call.Body!).RootElement;
        Assert.Equal("whatsapp", body.GetProperty("messaging_product").GetString());
        Assert.Equal("5511999998888", body.GetProperty("to").GetString());
        Assert.Equal("text", body.GetProperty("type").GetString());
        Assert.Equal("Olá!", body.GetProperty("text").GetProperty("body").GetString());
    }

    [Fact]
    public async Task Meta_error_body_is_in_the_exception()
    {
        var (client, _) = Build(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"message":"bad"}}""", Encoding.UTF8, "application/json"),
        }));

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendTextAsync("5511999998888", "Olá!", CancellationToken.None));

        Assert.Contains("bad", error.Message);
    }
}
