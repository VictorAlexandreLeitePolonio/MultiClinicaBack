using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Billing;

public sealed record SubscriptionCheckoutRequest(ClinicPlan Plan, BillingCycle Cycle);

public sealed record PurchaseRequest(string Item);

public sealed record BillingStatusDto(
    ClinicPlan Plan,
    BillingCycle? BillingCycle,
    bool IsFounder,
    int ExtraProfessionals,
    int? ProfessionalLimit,
    int ActiveProfessionals,
    bool HasSubscription);

public sealed record CheckoutUrlDto(string Url);
