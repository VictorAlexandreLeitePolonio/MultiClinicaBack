using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiClinica.API.Services.Billing;
using Xunit;

namespace MultiClinica.Tests;

public class AbacatePayClientTests
{
    private const string ApiKey = "chave-teste";
    private const string WebhookSecret = "s3cret";
    private const string HmacKey = "k";

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Method, string Path, string? Authorization, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString(), body));
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static FakeHandler SuccessHandler() => new(request => request.RequestUri!.AbsolutePath switch
    {
        "/v2/customers/create" => Json("""{"data":{"id":"cust_1"},"success":true,"error":null}"""),
        "/v2/products/create" => Json("""{"data":{"id":"prod_dyn"},"success":true,"error":null}"""),
        "/v2/subscriptions/create" => Json("""{"data":{"id":"bill_1","url":"https://pay.test/checkout"},"success":true,"error":null}"""),
        "/v2/checkouts/create" => Json("""{"data":{"id":"bill_2","url":"https://pay.test/checkout"},"success":true,"error":null}"""),
        "/v2/subscriptions/change-plan" => Json("""{"data":{"id":"subu_1","status":"PENDING"},"success":true,"error":null}"""),
        "/v2/subscriptions/cancel" => Json("""{"data":{"id":"subs_1","status":"CANCELLED"},"success":true,"error":null}"""),
        _ => new HttpResponseMessage(HttpStatusCode.NotFound),
    });

    private static (AbacatePayClient Client, FakeHandler Handler) Build(FakeHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.abacatepay.com") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        var options = Options.Create(new AbacatePayOptions
        {
            BaseUrl = "https://api.abacatepay.com",
            ApiKey = ApiKey,
            WebhookSecret = WebhookSecret,
            WebhookHmacKey = HmacKey,
            ReturnUrl = "https://app.test/configuracoes/plano?checkout=success",
        });
        return (new AbacatePayClient(http, options), handler);
    }

    [Fact]
    public async Task CreateSubscriptionCheckoutAsync_usa_campos_documentados()
    {
        var (client, handler) = Build(SuccessHandler());

        var url = await client.CreateSubscriptionCheckoutAsync(
            "prod_pro_m", 42, new AbacateCustomer("Clínica Teste", "admin@teste.com", "11999999999", null));

        Assert.Equal("https://pay.test/checkout", url);
        var call = handler.Calls.Single(c => c.Path == "/v2/subscriptions/create");
        Assert.Equal("Bearer chave-teste", call.Authorization);
        var body = JsonDocument.Parse(call.Body!).RootElement;
        Assert.Equal("prod_pro_m", body.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("clinica:42", body.GetProperty("externalId").GetString());
        Assert.Equal("cust_1", body.GetProperty("customerId").GetString());
        Assert.Equal("https://app.test/configuracoes/plano?checkout=success",
            body.GetProperty("completionUrl").GetString());
    }

    [Fact]
    public async Task CreateSubscriptionCheckoutAsync_reaproveita_customer_existente()
    {
        var (client, handler) = Build(SuccessHandler());

        var url = await client.CreateSubscriptionCheckoutAsync(
            "prod_pro_m", 42,
            new AbacateCustomer("Clínica Teste", "admin@teste.com", null, null, "cust_existente"));

        Assert.Equal("https://pay.test/checkout", url);
        Assert.DoesNotContain(handler.Calls, call => call.Path == "/v2/customers/create");
        var body = JsonDocument.Parse(handler.Calls.Single(c => c.Path == "/v2/subscriptions/create").Body!).RootElement;
        Assert.Equal("cust_existente", body.GetProperty("customerId").GetString());
    }

    [Fact]
    public async Task CreateOneTimeCheckoutAsync_envia_valor_em_centavos_e_referencia()
    {
        var (client, handler) = Build(SuccessHandler());

        var url = await client.CreateOneTimeCheckoutAsync(
            42, "pack:pro-conversations-200", "Pacote +200 conversas (válido até o fim deste mês)", 29m,
            new AbacateCustomer("Clínica Teste", "admin@teste.com", null, null));

        Assert.Equal("https://pay.test/checkout", url);

        var product = JsonDocument.Parse(handler.Calls.Single(c => c.Path == "/v2/products/create").Body!).RootElement;
        Assert.Equal(2900, product.GetProperty("price").GetInt32());
        Assert.Contains("válido até o fim deste mês", product.GetProperty("name").GetString());

        var checkout = JsonDocument.Parse(handler.Calls.Single(c => c.Path == "/v2/checkouts/create").Body!).RootElement;
        Assert.Equal("clinica:42|pack:pro-conversations-200", checkout.GetProperty("externalId").GetString());
        Assert.Equal("prod_dyn", checkout.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("cust_1", checkout.GetProperty("customerId").GetString());
    }

    [Fact]
    public async Task CreateOneTimeCheckoutAsync_reaproveita_customer_existente()
    {
        var (client, handler) = Build(SuccessHandler());

        await client.CreateOneTimeCheckoutAsync(
            42, "extra-professional", "Vaga extra de profissional", 59m,
            new AbacateCustomer("Clínica Teste", "admin@teste.com", null, null, "cust_existente"));

        Assert.DoesNotContain(handler.Calls, call => call.Path == "/v2/customers/create");
        var checkout = JsonDocument.Parse(handler.Calls.Single(c => c.Path == "/v2/checkouts/create").Body!).RootElement;
        Assert.Equal("cust_existente", checkout.GetProperty("customerId").GetString());
    }

    [Fact]
    public async Task CancelSubscriptionAsync_envia_o_id_no_corpo()
    {
        var (client, handler) = Build(SuccessHandler());

        await client.CancelSubscriptionAsync("subs_1");

        var call = handler.Calls.Single(c => c.Path == "/v2/subscriptions/cancel");
        var body = JsonDocument.Parse(call.Body!).RootElement;
        Assert.Equal("subs_1", body.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Resposta_nao_2xx_lanca_HttpRequestException()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("erro"),
        });
        var (client, _) = Build(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.CancelSubscriptionAsync("subs_1"));
    }

    private static AbacatePayOptions WebhookOptions() => new()
    {
        WebhookSecret = WebhookSecret,
        WebhookHmacKey = HmacKey,
    };

    private static string Sign(string body, string key = HmacKey) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(body)));

    [Fact]
    public void Webhook_autentico_com_segredo_e_hmac_corretos()
    {
        const string body = """{"id":"log_1"}""";

        Assert.True(AbacateWebhook.IsAuthentic(WebhookOptions(), WebhookSecret, body, Sign(body)));
    }

    [Fact]
    public void Webhook_recusa_segredo_errado()
    {
        const string body = """{"id":"log_1"}""";

        Assert.False(AbacateWebhook.IsAuthentic(WebhookOptions(), "outro", body, Sign(body)));
    }

    [Fact]
    public void Webhook_recusa_hmac_errado()
    {
        const string body = """{"id":"log_1"}""";

        Assert.False(AbacateWebhook.IsAuthentic(WebhookOptions(), WebhookSecret, body, Sign(body, "outra-chave")));
    }

    [Fact]
    public void Webhook_recusa_sem_header()
    {
        Assert.False(AbacateWebhook.IsAuthentic(WebhookOptions(), WebhookSecret, """{"id":"log_1"}""", null));
    }

    [Fact]
    public void Parse_subscription_completed()
    {
        const string payload = """
            {
              "id": "log_1",
              "event": "subscription.completed",
              "apiVersion": 2,
              "devMode": false,
              "data": {
                "subscription": {
                  "id": "subs_1", "amount": 79900, "currency": "BRL", "method": "CARD",
                  "status": "ACTIVE", "frequency": "ANNUALLY"
                },
                "customer": { "id": "cust_1", "name": "Clínica Teste", "email": "admin@teste.com" },
                "payment": { "id": "char_1", "externalId": "clinica:42" },
                "checkout": {
                  "id": "bill_1", "externalId": "clinica:42", "frequency": "SUBSCRIPTION",
                  "items": [{ "id": "prod_prem_a", "quantity": 1 }],
                  "status": "PAID", "customerId": "cust_1"
                }
              }
            }
            """;

        var parsed = AbacateWebhook.Parse(payload);

        Assert.NotNull(parsed);
        Assert.Equal("log_1", parsed.Id);
        Assert.Equal("subscription.completed", parsed.Type);
        Assert.Equal(42, parsed.ClinicaId);
        Assert.Null(parsed.Item);
        Assert.Equal("subs_1", parsed.SubscriptionId);
        Assert.Equal("cust_1", parsed.CustomerId);
        Assert.Equal("prod_prem_a", parsed.ProductId);
    }

    [Fact]
    public void Parse_subscription_cancelled()
    {
        const string payload = """
            {
              "id": "log_2",
              "event": "subscription.cancelled",
              "apiVersion": 2,
              "devMode": false,
              "data": {
                "subscription": {
                  "id": "subs_2", "amount": 4990, "currency": "BRL", "method": "CARD",
                  "status": "CANCELLED", "frequency": "MONTHLY", "cancelledDueTo": null
                },
                "customer": { "id": "cust_2", "name": "Clínica Teste", "email": "admin@teste.com" },
                "checkout": {
                  "id": "bill_2", "externalId": "clinica:7", "frequency": "SUBSCRIPTION",
                  "items": [{ "id": "prod_pro_m", "quantity": 1 }], "status": "PAID"
                }
              }
            }
            """;

        var parsed = AbacateWebhook.Parse(payload);

        Assert.NotNull(parsed);
        Assert.Equal("subscription.cancelled", parsed.Type);
        Assert.Equal(7, parsed.ClinicaId);
        Assert.Equal("subs_2", parsed.SubscriptionId);
        Assert.Equal("cust_2", parsed.CustomerId);
        Assert.Equal("prod_pro_m", parsed.ProductId);
    }

    [Fact]
    public void Parse_checkout_completed_avulso()
    {
        const string payload = """
            {
              "id": "log_3",
              "event": "checkout.completed",
              "apiVersion": 2,
              "devMode": false,
              "data": {
                "checkout": {
                  "id": "bill_3", "externalId": "clinica:42|pack:pro-conversations-200",
                  "frequency": "ONE_TIME", "items": [{ "id": "prod_pack", "quantity": 1 }],
                  "status": "PAID", "customerId": "cust_3"
                },
                "customer": { "id": "cust_3", "name": "Clínica Teste", "email": "admin@teste.com" }
              }
            }
            """;

        var parsed = AbacateWebhook.Parse(payload);

        Assert.NotNull(parsed);
        Assert.Equal("checkout.completed", parsed.Type);
        Assert.Equal(42, parsed.ClinicaId);
        Assert.Equal("pack:pro-conversations-200", parsed.Item);
        Assert.Null(parsed.SubscriptionId);
        Assert.Equal("cust_3", parsed.CustomerId);
        Assert.Equal("prod_pack", parsed.ProductId);
    }

    [Fact]
    public void Parse_recusa_payload_desconhecido()
    {
        Assert.Null(AbacateWebhook.Parse("""{"foo":"bar"}"""));
        Assert.Null(AbacateWebhook.Parse("não é json"));
    }
}
