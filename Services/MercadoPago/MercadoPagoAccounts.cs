using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.MercadoPago;

public sealed record ConnectedMercadoPago(ClinicMercadoPagoAccount Account, string AccessToken);

// Acesso à conta conectada da clínica, compartilhado pelo OAuth e pelos pagamentos.
public sealed class MercadoPagoAccounts(AppDbContext db, TokenCipher cipher, ILogger<MercadoPagoAccounts> logger)
{
    // Null se a clínica não conectou, foi revogada ou a chave de criptografia não está configurada.
    public async Task<ConnectedMercadoPago?> GetConnectedAsync(int clinicaId, CancellationToken ct = default)
    {
        if (!cipher.IsConfigured) return null;
        var account = await db.ClinicMercadoPagoAccounts
            .FirstOrDefaultAsync(a => a.ClinicaId == clinicaId && a.Status == MercadoPagoAccountStatus.Connected, ct);
        return account is null ? null : new(account, cipher.Decrypt(account.AccessTokenEncrypted));
    }

    // Chamar quando o Mercado Pago responder 401 com o token da clínica.
    public async Task MarkRevokedAsync(int clinicaId, CancellationToken ct = default)
    {
        await db.ClinicMercadoPagoAccounts.Where(a => a.ClinicaId == clinicaId)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.Status, MercadoPagoAccountStatus.Revoked), ct);
        logger.LogWarning("Conta Mercado Pago da clínica {ClinicaId} marcada como revogada", clinicaId);
    }
}
