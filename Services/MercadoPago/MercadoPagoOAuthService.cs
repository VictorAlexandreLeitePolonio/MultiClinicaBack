using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.MercadoPago;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.MercadoPago;

// Conecta a conta Mercado Pago da clínica por OAuth + PKCE. Só o hash do state é gravado.
public sealed class MercadoPagoOAuthService(
    AppDbContext db,
    IMercadoPagoClient client,
    TokenCipher cipher,
    IOptions<MercadoPagoOptions> options,
    TimeProvider time,
    ILogger<MercadoPagoOAuthService> logger)
{
    private const string InvalidSettings = "INVALID_PAYMENT_SETTINGS";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    public async Task<string> CreateAuthorizationUrlAsync(int clinicaId, int userId)
    {
        var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        db.MercadoPagoOAuthStates.Add(new MercadoPagoOAuthState
        {
            StateHash = Hash(state),
            ClinicaId = clinicaId,
            UserId = userId,
            CodeVerifier = verifier,
            ExpiresAt = time.GetUtcNow().UtcDateTime.Add(StateLifetime),
        });
        await db.SaveChangesAsync();

        var query = string.Join('&',
            $"client_id={Uri.EscapeDataString(options.Value.ClientId)}",
            "response_type=code",
            "platform_id=mp",
            $"state={Uri.EscapeDataString(state)}",
            $"redirect_uri={Uri.EscapeDataString(options.Value.RedirectUri)}",
            $"code_challenge={Uri.EscapeDataString(challenge)}",
            "code_challenge_method=S256");

        return $"{options.Value.AuthBaseUrl.TrimEnd('/')}/authorization?{query}";
    }

    // Devolve o sufixo do redirecionamento: conectado, erro ou conta-em-uso.
    public async Task<string> CompleteAsync(string? code, string? state)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state)) return "erro";

        try
        {
            var hash = Hash(state);
            var pending = await db.MercadoPagoOAuthStates
                .FirstOrDefaultAsync(s => s.StateHash == hash);
            if (pending is null) return "erro";

            // Uso único: consome antes de trocar o code, mesmo se a troca falhar.
            db.MercadoPagoOAuthStates.Remove(pending);
            await db.SaveChangesAsync();

            var now = time.GetUtcNow().UtcDateTime;
            if (pending.ExpiresAt <= now) return "erro";

            var token = await client.ExchangeCodeAsync(code, pending.CodeVerifier, CancellationToken.None);

            var otherClinic = await db.ClinicMercadoPagoAccounts
                .AnyAsync(a => a.MpUserId == token.UserId && a.ClinicaId != pending.ClinicaId);
            if (otherClinic) return "conta-em-uso";

            var account = await db.ClinicMercadoPagoAccounts
                .FirstOrDefaultAsync(a => a.ClinicaId == pending.ClinicaId);
            if (account is null)
            {
                account = new ClinicMercadoPagoAccount
                {
                    ClinicaId = pending.ClinicaId,
                    ConnectedAt = now,
                };
                db.ClinicMercadoPagoAccounts.Add(account);
            }

            account.MpUserId = token.UserId;
            account.PublicKey = token.PublicKey;
            account.AccessTokenEncrypted = cipher.Encrypt(token.AccessToken);
            account.RefreshTokenEncrypted = cipher.Encrypt(token.RefreshToken);
            account.ExpiresAt = now.AddSeconds(token.ExpiresIn);
            account.LiveMode = token.LiveMode;
            account.Status = MercadoPagoAccountStatus.Connected;
            account.UpdatedAt = now;
            await db.SaveChangesAsync();

            return "conectado";
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Falha ao concluir o OAuth do Mercado Pago");
            return "erro";
        }
    }

    public async Task<MercadoPagoStatusDto> GetStatusAsync(int clinicaId)
    {
        var clinic = await db.Clinicas
            .Where(c => c.Id == clinicaId)
            .Select(c => new
            {
                c.OnlineAcceptPix,
                c.OnlineAcceptCredit,
                c.OnlineMaxInstallments,
                c.OnlineAcceptDebit,
            })
            .FirstAsync();

        var account = await db.ClinicMercadoPagoAccounts
            .FirstOrDefaultAsync(a => a.ClinicaId == clinicaId);

        return new MercadoPagoStatusDto(
            Connected: account?.Status == MercadoPagoAccountStatus.Connected,
            LiveMode: account?.LiveMode ?? false,
            Revoked: account?.Status == MercadoPagoAccountStatus.Revoked,
            clinic.OnlineAcceptPix,
            clinic.OnlineAcceptCredit,
            clinic.OnlineMaxInstallments,
            clinic.OnlineAcceptDebit);
    }

    public async Task<Result<MercadoPagoSettingsDto>> UpdateSettingsAsync(int clinicaId, MercadoPagoSettingsDto dto)
    {
        if (!dto.AcceptPix && !dto.AcceptCredit && !dto.AcceptDebit)
            return Result<MercadoPagoSettingsDto>.Fail(InvalidSettings,
                "Escolha ao menos uma forma de pagamento.");

        if (dto.MaxInstallments is < 1 or > 12)
            return Result<MercadoPagoSettingsDto>.Fail(InvalidSettings,
                "O máximo de parcelas deve ficar entre 1 e 12.");

        var clinic = await db.Clinicas.FirstOrDefaultAsync(c => c.Id == clinicaId);
        if (clinic is null)
            return Result<MercadoPagoSettingsDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        clinic.OnlineAcceptPix = dto.AcceptPix;
        clinic.OnlineAcceptCredit = dto.AcceptCredit;
        clinic.OnlineMaxInstallments = dto.MaxInstallments;
        clinic.OnlineAcceptDebit = dto.AcceptDebit;
        await db.SaveChangesAsync();

        return Result<MercadoPagoSettingsDto>.Ok(dto);
    }

    public async Task DisconnectAsync(int clinicaId)
    {
        var accounts = await db.ClinicMercadoPagoAccounts
            .Where(a => a.ClinicaId == clinicaId)
            .ToListAsync();
        if (accounts.Count == 0) return;

        db.ClinicMercadoPagoAccounts.RemoveRange(accounts);
        await db.SaveChangesAsync();
    }

    private static string Hash(string state) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
}
