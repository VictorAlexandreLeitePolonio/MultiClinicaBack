namespace MultiClinica.API.Services.MercadoPago;

// Cada trilha registra seus serviços no próprio método, para não disputarem o Program.cs.
public static class MercadoPagoRegistration
{
    // Trilha A: MercadoPagoClient (HttpClient), MercadoPagoOAuthService, MercadoPagoTokenRefresher.
    public static IServiceCollection AddMercadoPagoOAuth(this IServiceCollection services, IHostEnvironment environment)
    {
        return services;
    }

    // Trilha B: MercadoPagoPaymentSync, OnlinePaymentService.
    public static IServiceCollection AddMercadoPagoPayments(this IServiceCollection services)
    {
        return services;
    }
}
