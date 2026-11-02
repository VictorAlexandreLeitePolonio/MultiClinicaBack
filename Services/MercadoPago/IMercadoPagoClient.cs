namespace MultiClinica.API.Services.MercadoPago;

public sealed record MpUserInfo(long Id, string? Nickname, string? Email);

public sealed record MpTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, long UserId, string PublicKey, bool LiveMode);

// PaymentMethodId: "pix", "visa", "master", "debvisa"... (vem do formData do Payment Brick).
public sealed record MpPaymentRequest(
    decimal TransactionAmount,
    string Description,
    string ExternalReference,
    string NotificationUrl,
    string PaymentMethodId,
    string? CardToken,
    int? Installments,
    string? IssuerId,
    string PayerEmail,
    string? PayerIdentificationType,
    string? PayerIdentificationNumber,
    DateTime? DateOfExpiration);

// PaymentTypeId: "bank_transfer" (Pix), "credit_card", "debit_card".
public sealed record MpPayment(
    long Id,
    string Status,
    string? StatusDetail,
    string? ExternalReference,
    long? CollectorId,
    string? PaymentTypeId,
    string? PaymentMethodId,
    DateTime? DateApproved);

// Erro HTTP do Mercado Pago com o corpo da resposta (401 = token revogado).
public sealed class MercadoPagoApiException(int statusCode, string body)
    : Exception($"Mercado Pago respondeu {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}

public interface IMercadoPagoClient
{
    // POST /oauth/token grant_type=authorization_code (com code_verifier do PKCE).
    Task<MpTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);
    // POST /oauth/token grant_type=refresh_token. Devolve um refresh_token NOVO: grave-o.
    Task<MpTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct);
    // POST /v1/payments com o token da clínica e X-Idempotency-Key.
    Task<MpPayment> CreatePaymentAsync(string accessToken, MpPaymentRequest request, string idempotencyKey, CancellationToken ct);
    // GET /v1/payments/{id} com o token da clínica.
    Task<MpPayment> GetPaymentAsync(string accessToken, long paymentId, CancellationToken ct);

    // GET /users/me com o token da clínica: apelido e e-mail da conta conectada.
    Task<MpUserInfo> GetMeAsync(string accessToken, CancellationToken ct);
    // POST /v1/payments/{id}/refunds sem corpo = reembolso total.
    Task RefundAsync(string accessToken, long paymentId, string idempotencyKey, CancellationToken ct);
}
