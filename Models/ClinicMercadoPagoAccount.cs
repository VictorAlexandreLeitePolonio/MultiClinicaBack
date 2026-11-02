namespace MultiClinica.API.Models;

public enum MercadoPagoAccountStatus { Connected, Revoked }

// Conta Mercado Pago da clínica conectada por OAuth. Tokens só criptografados (TokenCipher).
public class ClinicMercadoPagoAccount
{
    public int Id { get; set; }
    public int ClinicaId { get; set; }
    public long MpUserId { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public string AccessTokenEncrypted { get; set; } = string.Empty;
    public string RefreshTokenEncrypted { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool LiveMode { get; set; }
    public MercadoPagoAccountStatus Status { get; set; } = MercadoPagoAccountStatus.Connected;
    public DateTime ConnectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Clinica Clinica { get; set; } = null!;
}

// State do OAuth: uso único, 10 minutos. Só o hash do state fica no banco.
public class MercadoPagoOAuthState
{
    public int Id { get; set; }
    public string StateHash { get; set; } = string.Empty;
    public int ClinicaId { get; set; }
    public int UserId { get; set; }
    public string CodeVerifier { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
