using System.Text.Json.Serialization;

namespace MultiClinica.API.DTOs.Payment;

// GET api/payments/{id}/link
public sealed class PaymentLinkDto
{
    public string Url { get; set; } = string.Empty;
}

// GET api/public/payments/{token}
public sealed class PublicPaymentDto
{
    public string ClinicName { get; set; } = string.Empty;
    public string? ClinicLogoUrl { get; set; }
    public string PatientFirstName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly ReferenceMonth { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? MpStatus { get; set; }
    public long? MpPaymentId { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public bool AcceptPix { get; set; }
    public bool AcceptCredit { get; set; }
    public int MaxInstallments { get; set; }
    public bool AcceptDebit { get; set; }
}

// POST api/public/payments/{token}/pay — corpo é o formData do Payment Brick (snake_case).
public sealed class PublicPayRequest
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("payment_method_id")]
    public string? PaymentMethodId { get; set; }

    [JsonPropertyName("payment_type_id")]
    public string? PaymentTypeId { get; set; }

    [JsonPropertyName("issuer_id")]
    public string? IssuerId { get; set; }

    [JsonPropertyName("installments")]
    public int? Installments { get; set; }

    [JsonPropertyName("payer")]
    public PublicPayerDto? Payer { get; set; }
}

public sealed class PublicPayerDto
{
    public string? Email { get; set; }
    public PublicPayerIdentificationDto? Identification { get; set; }
}

public sealed class PublicPayerIdentificationDto
{
    public string? Type { get; set; }
    public string? Number { get; set; }
}

// POST api/public/payments/{token}/pay
public sealed class PublicPayResultDto
{
    // Id do pagamento no Mercado Pago (vai para o Status Screen Brick).
    public long PaymentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

// GET api/patient/payments
public sealed class PatientChargeDto
{
    public int Id { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly ReferenceMonth { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PayUrl { get; set; } = string.Empty;
}
