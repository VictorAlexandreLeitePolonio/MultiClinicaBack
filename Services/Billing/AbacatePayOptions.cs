namespace MultiClinica.API.Services.Billing;

public sealed class AbacatePayOptions
{
    public string BaseUrl { get; set; } = "https://api.abacatepay.com";
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;   // vai na query ?webhookSecret=
    public string WebhookHmacKey { get; set; } = string.Empty;  // chave do X-Webhook-Signature (chave pública da doc por padrão)
    public string ReturnUrl { get; set; } = string.Empty;       // front: {FRONTEND_URL}/app/configuracoes/plano?checkout=success
    // Chaves: "Pro_Monthly", "Premium_Annual", "extra-professional", "pack_<id>".
    // Sem ':' porque na configuração do .NET ':' vira hierarquia e o id nunca seria lido.
    public Dictionary<string, string> Products { get; set; } = [];
    // Pix Automático só depois de liberado na loja do AbacatePay; aí configure ["CARD", "PIX"].
    // Vazio por padrão: com default no array, o binder do .NET acrescentaria em vez de substituir.
    public string[] SubscriptionMethods { get; set; } = [];

    public string[] EffectiveSubscriptionMethods => SubscriptionMethods.Length > 0 ? SubscriptionMethods : ["CARD"];

    public static string ProductKey(string name) => name.Replace(':', '_');

    public string? ProductFor(string name)
        => Products.TryGetValue(ProductKey(name), out var id) && !string.IsNullOrWhiteSpace(id) ? id : null;
}

public sealed record AbacateCustomer(
    string Name, string Email, string? Phone, string? TaxId, string? ExistingId = null);

public interface IAbacatePayClient
{
    /// Cria o checkout de assinatura; devolve a URL do checkout. `externalId` = id da clínica.
    Task<string> CreateSubscriptionCheckoutAsync(string productId, int clinicaId, AbacateCustomer customer, CancellationToken ct = default);
    /// Cobrança avulsa (pacote ou vaga). `item` vai no metadata/externalId junto do id da clínica.
    Task<string> CreateOneTimeCheckoutAsync(int clinicaId, string item, string description, decimal amount, AbacateCustomer customer, CancellationToken ct = default);
    Task ChangeSubscriptionAsync(string subscriptionId, string productId, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default);
}
