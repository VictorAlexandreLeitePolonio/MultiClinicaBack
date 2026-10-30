namespace MultiClinica.API.Services.Billing;

public sealed class AbacatePayOptions
{
    public string BaseUrl { get; set; } = "https://api.abacatepay.com";
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;   // vai na query ?webhookSecret=
    public string WebhookHmacKey { get; set; } = string.Empty;  // chave do X-Webhook-Signature (chave pública da doc por padrão)
    public string ReturnUrl { get; set; } = string.Empty;       // front: {FRONTEND_URL}/app/configuracoes/plano?checkout=success
    // Products["Pro:Monthly"], ["Pro:Annual"], ["Premium:Monthly"], ["Premium:Annual"]
    public Dictionary<string, string> Products { get; set; } = [];
}

public sealed record AbacateCustomer(string Name, string Email, string? Phone, string? TaxId);

public interface IAbacatePayClient
{
    /// Cria o checkout de assinatura; devolve a URL do checkout. `externalId` = id da clínica.
    Task<string> CreateSubscriptionCheckoutAsync(string productId, int clinicaId, AbacateCustomer customer, CancellationToken ct = default);
    /// Cobrança avulsa (pacote ou vaga). `item` vai no metadata/externalId junto do id da clínica.
    Task<string> CreateOneTimeCheckoutAsync(int clinicaId, string item, string description, decimal amount, AbacateCustomer customer, CancellationToken ct = default);
    Task ChangeSubscriptionAsync(string subscriptionId, string productId, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default);
}
