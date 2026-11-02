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
    bool AcceptDebit);
