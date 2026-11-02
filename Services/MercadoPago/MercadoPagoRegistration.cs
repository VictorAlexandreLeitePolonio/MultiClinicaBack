using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.MercadoPago;

// Cada trilha registra seus serviços no próprio método, para não disputarem o Program.cs.
public static class MercadoPagoRegistration
{
    // Trilha A: MercadoPagoClient (HttpClient), MercadoPagoOAuthService, MercadoPagoTokenRefresher.
    public static IServiceCollection AddMercadoPagoOAuth(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddHttpClient<IMercadoPagoClient, MercadoPagoClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<MercadoPagoOptions>>().Value;
            client.BaseAddress = new Uri(options.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
        }).RemoveAllLoggers();

        services.AddScoped<MercadoPagoOAuthService>();

        // O renovador roda como singleton: ele mesmo abre escopo por passada.
        services.AddSingleton<MercadoPagoTokenRefresher>();
        if (!environment.IsEnvironment("Testing"))
            services.AddHostedService(sp => sp.GetRequiredService<MercadoPagoTokenRefresher>());

        return services;
    }

    // Trilha B: MercadoPagoPaymentSync, OnlinePaymentService.
    public static IServiceCollection AddMercadoPagoPayments(this IServiceCollection services)
    {
        services.AddScoped<MercadoPagoPaymentSync>();
        services.AddScoped<OnlinePaymentService>();
        return services;
    }
}
