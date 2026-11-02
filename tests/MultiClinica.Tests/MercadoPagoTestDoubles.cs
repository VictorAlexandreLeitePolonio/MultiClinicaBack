using MultiClinica.API.Services.MercadoPago;

namespace MultiClinica.Tests;

public sealed class FakeMercadoPagoClient : IMercadoPagoClient
{
    // Roteiros: preencha antes de chamar o endpoint; null = lança MercadoPagoApiException(500).
    public MpTokenResponse? NextToken { get; set; }
    public Func<MpPaymentRequest, MpPayment>? OnCreate { get; set; }
    public Dictionary<long, MpPayment> Payments { get; } = [];
    public int? FailWithStatus { get; set; }

    public List<(string Code, string Verifier)> Exchanges { get; } = [];
    public List<string> Refreshes { get; } = [];
    public List<(string AccessToken, MpPaymentRequest Request, string IdempotencyKey)> Created { get; } = [];
    public List<(string AccessToken, long PaymentId, string IdempotencyKey)> Refunds { get; } = [];

    public Task<MpTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct)
    {
        Fail();
        Exchanges.Add((code, codeVerifier));
        return Task.FromResult(NextToken ?? throw new MercadoPagoApiException(500, "sem token roteirizado"));
    }

    public Task<MpTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        Fail();
        Refreshes.Add(refreshToken);
        return Task.FromResult(NextToken ?? throw new MercadoPagoApiException(500, "sem token roteirizado"));
    }

    public Task<MpPayment> CreatePaymentAsync(string accessToken, MpPaymentRequest request, string idempotencyKey, CancellationToken ct)
    {
        Fail();
        Created.Add((accessToken, request, idempotencyKey));
        var payment = (OnCreate ?? throw new MercadoPagoApiException(500, "sem pagamento roteirizado"))(request);
        Payments[payment.Id] = payment;
        return Task.FromResult(payment);
    }

    public MpUserInfo? Me { get; set; }
    public List<string> MeRequests { get; } = [];

    public Task<MpUserInfo> GetMeAsync(string accessToken, CancellationToken ct)
    {
        MeRequests.Add(accessToken);
        return Me is null ? throw new MercadoPagoApiException(500, "sem /users/me roteirizado") : Task.FromResult(Me);
    }

    public Task<MpPayment> GetPaymentAsync(string accessToken, long paymentId, CancellationToken ct)
    {
        Fail();
        return Task.FromResult(Payments.TryGetValue(paymentId, out var p) ? p : throw new MercadoPagoApiException(404, "not found"));
    }

    public Task RefundAsync(string accessToken, long paymentId, string idempotencyKey, CancellationToken ct)
    {
        Fail();
        Refunds.Add((accessToken, paymentId, idempotencyKey));
        return Task.CompletedTask;
    }

    private void Fail()
    {
        if (FailWithStatus is { } status) throw new MercadoPagoApiException(status, "falha roteirizada");
    }
}
