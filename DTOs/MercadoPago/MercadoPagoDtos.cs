namespace MultiClinica.API.DTOs.MercadoPago;

public sealed record MercadoPagoConnectDto(string Url);

public sealed record MercadoPagoSettingsDto(
    bool AcceptPix,
    bool AcceptCredit,
    int MaxInstallments,
    bool AcceptDebit);

public sealed record MercadoPagoStatusDto(
    bool Connected,
    bool LiveMode,
    bool Revoked,
    bool AcceptPix,
    bool AcceptCredit,
    int MaxInstallments,
    bool AcceptDebit,
    // Conta conectada (Trilha B preenche): id do MP, apelido, e-mail e quando conectou.
    long? AccountId = null,
    string? Nickname = null,
    string? Email = null,
    DateTime? ConnectedAt = null);
