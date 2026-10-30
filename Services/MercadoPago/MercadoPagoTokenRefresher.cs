using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.MercadoPago;

// Renova o access token das contas conectadas antes do vencimento (o refresh token também muda).
public sealed class MercadoPagoTokenRefresher(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<MercadoPagoTokenRefresher> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha no ciclo de renovação do Mercado Pago");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task RefreshDueAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<IMercadoPagoClient>();
        var cipher = scope.ServiceProvider.GetRequiredService<TokenCipher>();

        var now = time.GetUtcNow().UtcDateTime;
        var due = await db.ClinicMercadoPagoAccounts
            .Where(a => a.Status == MercadoPagoAccountStatus.Connected && a.ExpiresAt < now.Add(RefreshWindow))
            .ToListAsync(ct);

        foreach (var account in due)
        {
            try
            {
                var refreshToken = cipher.Decrypt(account.RefreshTokenEncrypted);
                var token = await client.RefreshAsync(refreshToken, ct);

                account.AccessTokenEncrypted = cipher.Encrypt(token.AccessToken);
                account.RefreshTokenEncrypted = cipher.Encrypt(token.RefreshToken);
                account.ExpiresAt = now.AddSeconds(token.ExpiresIn);
                account.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
            }
            catch (MercadoPagoApiException exception) when (exception.StatusCode is 400 or 401 or 403)
            {
                account.Status = MercadoPagoAccountStatus.Revoked;
                account.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Conta Mercado Pago da clínica {ClinicaId} revogada: {Status} {Body}",
                    account.ClinicaId, exception.StatusCode, exception.Body);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Erro de rede ou chave: tenta de novo no próximo ciclo.
                logger.LogError(exception, "Falha ao renovar o Mercado Pago da clínica {ClinicaId}", account.ClinicaId);
            }
        }
    }
}
