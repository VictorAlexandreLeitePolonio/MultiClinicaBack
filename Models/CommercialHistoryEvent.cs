namespace MultiClinica.API.Models;

public enum CommercialHistoryEventType
{
    ClinicCreated,
    BillingConfigChanged,
    ChargeCreated,
    PaymentRegistered,
    PaymentCancelled,
    AutomaticBillingBlock,
    ManualBillingUnblock,
    ClinicActivated,
    ClinicDeactivated,
    AdminUserCreated
}

public class CommercialHistoryEvent : AuditableEntity
{
    public int ClinicaId { get; set; }
    public CommercialHistoryEventType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }

    public Clinica Clinica { get; set; } = null!;
}
