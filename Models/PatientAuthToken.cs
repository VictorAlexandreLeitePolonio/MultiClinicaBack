namespace MultiClinica.API.Models;

public enum PatientAuthTokenType
{
    Activation,
    PasswordReset
}

/// <summary>
/// Token de uso único para ativação de conta / redefinição de senha do paciente.
/// Apenas o <see cref="TokenHash"/> é persistido — o token puro só existe no
/// link enviado por e-mail.
/// </summary>
public class PatientAuthToken
{
    public int Id { get; set; }
    public int PatientAccountId { get; set; }
    public PatientAccount PatientAccount { get; set; } = null!;

    public PatientAuthTokenType Type { get; set; }

    /// <summary>Hash SHA-256 (hex) do token puro.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsUsable(DateTime now) => ConsumedAt is null && ExpiresAt > now;
}
