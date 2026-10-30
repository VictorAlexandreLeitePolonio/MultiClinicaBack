using MultiClinica.API.Models;

namespace MultiClinica.API.Common;

public static class Features
{
    public const string OwnerCopilot = "OwnerCopilot";
    public const string OnlinePayments = "OnlinePayments";
    public const string WhatsAppReminders = "WhatsAppReminders";
    public const string PatientChatbot = "PatientChatbot";
    public const string Transcription = "Transcription";
}

public sealed record PlanLimits(
    IReadOnlySet<string> Features,
    int? IncludedProfessionals, // null = ilimitado
    int MaxExtraProfessionals,
    int WhatsAppConversationsPerMonth,
    int TranscriptionMinutesPerMonth,
    int HubRank,
    decimal MonthlyPrice);

public sealed record UsagePack(string Id, ClinicPlan Plan, UsageMetric Metric, int Amount, decimal Price);

// Fonte única de planos, preços e limites (spec 2026-10-30-planos-plataforma).
public static class PlanMatrix
{
    public const decimal ExtraProfessionalPrice = 59m;

    private static readonly IReadOnlyDictionary<ClinicPlan, PlanLimits> Map = new Dictionary<ClinicPlan, PlanLimits>
    {
        [ClinicPlan.Free] = new(new HashSet<string>(), 1, 1, 0, 0, 0, 0m),
        [ClinicPlan.Pro] = new(new HashSet<string> { Features.OwnerCopilot, Features.OnlinePayments }, 3, 2, 300, 0, 1, 49.90m),
        [ClinicPlan.Premium] = new(new HashSet<string>
        {
            Features.OwnerCopilot, Features.OnlinePayments, Features.WhatsAppReminders,
            Features.PatientChatbot, Features.Transcription,
        }, null, 0, 800, 1200, 2, 79.90m),
    };

    public static readonly IReadOnlyList<UsagePack> Packs =
    [
        new("pro-conversations-200", ClinicPlan.Pro, UsageMetric.WhatsAppConversations, 200, 29m),
        new("premium-conversations-500", ClinicPlan.Premium, UsageMetric.WhatsAppConversations, 500, 49m),
        new("premium-transcription-300", ClinicPlan.Premium, UsageMetric.TranscriptionMinutes, 300, 29m),
    ];

    public static PlanLimits For(ClinicPlan plan) => Map[plan];

    public static bool Has(ClinicPlan plan, string feature) => Map[plan].Features.Contains(feature);

    public static decimal AnnualPrice(ClinicPlan plan) => Map[plan].MonthlyPrice * 10;

    // Vaga comprada é permanente, mas só conta até o máximo do plano atual.
    public static int? ProfessionalLimit(ClinicPlan plan, int extraProfessionals)
    {
        var limits = Map[plan];
        return limits.IncludedProfessionals is int included
            ? included + Math.Min(extraProfessionals, limits.MaxExtraProfessionals)
            : null;
    }

    public static int MonthlyQuota(ClinicPlan plan, UsageMetric metric) => metric switch
    {
        UsageMetric.WhatsAppConversations => Map[plan].WhatsAppConversationsPerMonth,
        UsageMetric.TranscriptionMinutes => Map[plan].TranscriptionMinutesPerMonth,
        _ => 0,
    };

    public static ClinicPlan CheapestWith(string feature)
        => Enum.GetValues<ClinicPlan>().First(plan => Has(plan, feature));
}
