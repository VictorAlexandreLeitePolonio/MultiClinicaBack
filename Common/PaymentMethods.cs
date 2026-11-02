namespace MultiClinica.API.Common;

// Formas de pagamento aceitas em cobranças novas (contrato com o front).
// Cobranças antigas podem ter valores livres ("Cartão", "Boleto" etc.) e continuam editáveis
// sem trocar a forma; a validação de update fica no PaymentService.
public static class PaymentMethods
{
    public const string Online     = "Online (Mercado Pago)";
    public const string Cash       = "Dinheiro";
    public const string Pix        = "Pix";
    public const string CreditCard = "Cartão de crédito";
    public const string DebitCard  = "Cartão de débito";
    public const string Transfer   = "Transferência";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Online, Cash, Pix, CreditCard, DebitCard, Transfer,
    };

    public static readonly IReadOnlySet<string> Manual = new HashSet<string>(StringComparer.Ordinal)
    {
        Cash, Pix, CreditCard, DebitCard, Transfer,
    };

    public static bool IsKnown(string? method)
        => method is not null && All.Contains(method);

    public static bool IsOnline(string? method)
        => string.Equals(method, Online, StringComparison.Ordinal);
}
