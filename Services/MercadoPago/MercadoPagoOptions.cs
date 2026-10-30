namespace MultiClinica.API.Services.MercadoPago;

public sealed class MercadoPagoOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    // URL exata cadastrada no app do Mercado Pago: {API}/api/mercadopago/callback
    public string RedirectUri { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    // 32 bytes em base64 (openssl rand -base64 32). Perder a chave obriga todas as clínicas a reconectar.
    public string TokenEncryptionKey { get; set; } = string.Empty;
    // Dev com contas de teste: pede tokens de sandbox (test_token). Token de produção numa vendedora de teste
    // faz o MP recusar o pagamento com "Invalid users involved".
    public bool TestMode { get; set; }
    public string ApiBaseUrl { get; set; } = "https://api.mercadopago.com/";
    public string AuthBaseUrl { get; set; } = "https://auth.mercadopago.com/";
}
