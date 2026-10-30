using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.Billing;

// Documentação consultada:
// - https://docs.abacatepay.com/llms.txt
// - https://docs.abacatepay.com/pages/subscriptions/reference
// - https://docs.abacatepay.com/pages/subscriptions/create
// - https://docs.abacatepay.com/pages/subscriptions/change-plan
// - https://docs.abacatepay.com/pages/subscriptions/cancel
// - https://docs.abacatepay.com/pages/payment/create
// - https://docs.abacatepay.com/pages/client/create
// - https://docs.abacatepay.com/pages/products/create
// - https://docs.abacatepay.com/pages/webhooks/reference
// - https://docs.abacatepay.com/pages/webhooks/security
public sealed class AbacatePayClient(HttpClient http, IOptions<AbacatePayOptions> options) : IAbacatePayClient
{
    private readonly AbacatePayOptions _options = options.Value;

    public async Task<string> CreateSubscriptionCheckoutAsync(
        string productId, int clinicaId, AbacateCustomer customer, CancellationToken ct = default)
    {
        var customerId = await ResolveCustomerIdAsync(customer, ct);
        var body = CheckoutBody(clinicaId, $"clinica:{clinicaId}", customerId);
        body["items"] = new[] { new { id = productId, quantity = 1 } };
        body["methods"] = new[] { "CARD", "PIX" };

        using var response = await http.PostAsJsonAsync("v2/subscriptions/create", body, ct);
        return await ReadUrlAsync(response, ct);
    }

    public async Task<string> CreateOneTimeCheckoutAsync(
        int clinicaId, string item, string description, decimal amount, AbacateCustomer customer,
        CancellationToken ct = default)
    {
        var customerId = await ResolveCustomerIdAsync(customer, ct);
        var productId = await ResolveOneTimeProductAsync(item, description, amount, ct);
        var body = CheckoutBody(clinicaId, $"clinica:{clinicaId}|{item}", customerId);
        body["items"] = new[] { new { id = productId, quantity = 1 } };

        using var response = await http.PostAsJsonAsync("v2/checkouts/create", body, ct);
        return await ReadUrlAsync(response, ct);
    }

    public async Task ChangeSubscriptionAsync(string subscriptionId, string productId, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("v2/subscriptions/change-plan",
            new { id = subscriptionId, productId, quantity = 1 }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("v2/subscriptions/cancel",
            new { id = subscriptionId }, ct);
        response.EnsureSuccessStatusCode();
    }

    private Dictionary<string, object?> CheckoutBody(int clinicaId, string externalId, string customerId)
    {
        var body = new Dictionary<string, object?>
        {
            ["externalId"] = externalId,
            ["customerId"] = customerId,
            ["metadata"] = new Dictionary<string, string>
            {
                ["clinicaId"] = clinicaId.ToString(CultureInfo.InvariantCulture),
            },
        };

        if (!string.IsNullOrWhiteSpace(_options.ReturnUrl))
        {
            body["returnUrl"] = _options.ReturnUrl;
            body["completionUrl"] = _options.ReturnUrl;
        }

        return body;
    }

    private async Task<string> ResolveCustomerIdAsync(AbacateCustomer customer, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(customer.ExistingId))
            return customer.ExistingId;

        var body = new Dictionary<string, string> { ["email"] = customer.Email };
        if (!string.IsNullOrWhiteSpace(customer.Name))
            body["name"] = customer.Name;
        if (!string.IsNullOrWhiteSpace(customer.Phone))
            body["cellphone"] = customer.Phone;
        if (!string.IsNullOrWhiteSpace(customer.TaxId))
            body["taxId"] = customer.TaxId;

        using var response = await http.PostAsJsonAsync("v2/customers/create", body, ct);
        return await ReadDataIdAsync(response, "cliente", ct);
    }

    private async Task<string> ResolveOneTimeProductAsync(
        string item, string description, decimal amount, CancellationToken ct)
    {
        if (_options.ProductFor(item) is { } configured)
            return configured;

        // A API v2 só cria checkout a partir de produto cadastrado: a cobrança avulsa vira um produto de uso único.
        var body = new Dictionary<string, object?>
        {
            ["externalId"] = $"{item}-{Guid.NewGuid():N}",
            ["name"] = description,
            ["price"] = ToCents(amount),
            ["currency"] = "BRL",
        };

        using var response = await http.PostAsJsonAsync("v2/products/create", body, ct);
        return await ReadDataIdAsync(response, "produto", ct);
    }

    private static int ToCents(decimal amount) =>
        checked((int)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    private static async Task<string> ReadUrlAsync(HttpResponseMessage response, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        var data = await ReadDataAsync(response, ct);

        if (data.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
            return url.GetString()!;

        throw new InvalidOperationException("Resposta da AbacatePay sem URL de checkout.");
    }

    private static async Task<string> ReadDataIdAsync(
        HttpResponseMessage response, string resource, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        var data = await ReadDataAsync(response, ct);

        if (data.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            return id.GetString()!;

        throw new InvalidOperationException($"Resposta da AbacatePay sem id de {resource}.");
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            return data.Clone();

        throw new InvalidOperationException("Resposta da AbacatePay sem o envelope data.");
    }
}
