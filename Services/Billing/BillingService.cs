using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Billing;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services.Billing;

public class BillingService(
    AppDbContext db,
    IAbacatePayClient abacate,
    IOptions<AbacatePayOptions> options,
    ClinicPlanService plans,
    UsageService usage,
    IUsuarioLogadoService usuario)
{
    private const string WebhookProvider = "abacatepay";
    private const string FounderMessage = "Clínica fundadora já tem o Premium.";
    private const string ExtraProfessionalItem = "extra-professional";

    private readonly AbacatePayOptions _options = options.Value;

    public async Task<Result<BillingStatusDto>> GetStatusAsync()
    {
        var clinic = await db.Clinicas.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == usuario.ClinicaId && !item.IsDeleted);
        if (clinic is null)
            return Result<BillingStatusDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        return Result<BillingStatusDto>.Ok(new BillingStatusDto(
            clinic.Plan,
            clinic.BillingCycle,
            clinic.IsFounder,
            clinic.ExtraProfessionals,
            PlanMatrix.ProfessionalLimit(clinic.Plan, clinic.ExtraProfessionals),
            await plans.CountActiveProfessionalsAsync(clinic.Id),
            !string.IsNullOrWhiteSpace(clinic.AbacateSubscriptionId)));
    }

    public async Task<Result<CheckoutUrlDto>> CreateSubscriptionCheckoutAsync(SubscriptionCheckoutRequest dto)
    {
        var clinic = await CurrentClinicAsync();
        if (clinic is null)
            return Result<CheckoutUrlDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");
        if (clinic.IsFounder)
            return Result<CheckoutUrlDto>.Fail(ErrorCodes.CannotModify, FounderMessage);
        if (!string.IsNullOrWhiteSpace(clinic.AbacateSubscriptionId))
            return Result<CheckoutUrlDto>.Fail(ErrorCodes.CannotModify,
                "Já existe uma assinatura ativa. Use a troca de plano.");
        if (dto.Plan is not (ClinicPlan.Pro or ClinicPlan.Premium))
            return Result<CheckoutUrlDto>.Fail(ErrorCodes.InvalidValue, "Escolha os planos Pro ou Premium.");
        if (_options.ProductFor($"{dto.Plan}_{dto.Cycle}") is not { } productId)
            return Result<CheckoutUrlDto>.Fail(ErrorCodes.InvalidValue, "Plano indisponível para assinatura.");

        var url = await abacate.CreateSubscriptionCheckoutAsync(
            productId, clinic.Id, await CustomerAsync(clinic));
        return Result<CheckoutUrlDto>.Ok(new CheckoutUrlDto(url));
    }

    public async Task<Result<bool>> ChangeSubscriptionAsync(SubscriptionCheckoutRequest dto)
    {
        var clinic = await CurrentClinicAsync();
        if (clinic is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");
        if (clinic.IsFounder)
            return Result<bool>.Fail(ErrorCodes.CannotModify, FounderMessage);
        if (string.IsNullOrWhiteSpace(clinic.AbacateSubscriptionId))
            return Result<bool>.Fail(ErrorCodes.NotFound, "Clínica sem assinatura ativa.");
        if (dto.Plan is not (ClinicPlan.Pro or ClinicPlan.Premium))
            return Result<bool>.Fail(ErrorCodes.InvalidValue, "Escolha os planos Pro ou Premium.");
        if (_options.ProductFor($"{dto.Plan}_{dto.Cycle}") is not { } productId)
            return Result<bool>.Fail(ErrorCodes.InvalidValue, "Plano indisponível para assinatura.");

        await abacate.ChangeSubscriptionAsync(clinic.AbacateSubscriptionId, productId);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> CancelSubscriptionAsync()
    {
        var clinic = await CurrentClinicAsync();
        if (clinic is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");
        if (clinic.IsFounder)
            return Result<bool>.Fail(ErrorCodes.CannotModify, FounderMessage);
        if (string.IsNullOrWhiteSpace(clinic.AbacateSubscriptionId))
            return Result<bool>.Fail(ErrorCodes.NotFound, "Clínica sem assinatura ativa.");

        await abacate.CancelSubscriptionAsync(clinic.AbacateSubscriptionId);
        await plans.ApplyPlanAsync(clinic, ClinicPlan.Free, null);
        clinic.AbacateSubscriptionId = null;
        await db.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    public async Task<Result<CheckoutUrlDto>> PurchaseAsync(PurchaseRequest dto)
    {
        var clinic = await CurrentClinicAsync();
        if (clinic is null)
            return Result<CheckoutUrlDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");
        if (clinic.IsFounder)
            return Result<CheckoutUrlDto>.Fail(ErrorCodes.CannotModify, FounderMessage);

        if (string.IsNullOrWhiteSpace(dto.Item))
            return Result<CheckoutUrlDto>.Fail(ErrorCodes.InvalidValue, "Item inválido.");
        var item = dto.Item.Trim();
        decimal amount;
        string description;
        if (item == ExtraProfessionalItem)
        {
            if (clinic.ExtraProfessionals >= PlanMatrix.For(clinic.Plan).MaxExtraProfessionals)
                return Result<CheckoutUrlDto>.Fail(ErrorCodes.CannotModify,
                    "Seu plano não permite comprar mais vagas de profissional.");
            amount = PlanMatrix.ExtraProfessionalPrice;
            description = "Vaga extra de profissional (pagamento único)";
        }
        else if (item.StartsWith("pack:", StringComparison.Ordinal))
        {
            var pack = PlanMatrix.Packs.FirstOrDefault(candidate => candidate.Id == item["pack:".Length..]);
            if (pack is null)
                return Result<CheckoutUrlDto>.Fail(ErrorCodes.InvalidValue, "Pacote inválido.");
            if (pack.Plan != clinic.Plan)
                return Result<CheckoutUrlDto>.Fail(ErrorCodes.CannotModify,
                    "Este pacote não pertence ao seu plano atual.");
            amount = pack.Price;
            var metric = pack.Metric == UsageMetric.WhatsAppConversations ? "conversas" : "minutos de transcrição";
            description = $"Pacote +{pack.Amount} {metric} (válido até o fim deste mês)";
        }
        else
        {
            return Result<CheckoutUrlDto>.Fail(ErrorCodes.InvalidValue, "Item inválido.");
        }

        var url = await abacate.CreateOneTimeCheckoutAsync(
            clinic.Id, item, description, amount, await CustomerAsync(clinic));
        return Result<CheckoutUrlDto>.Ok(new CheckoutUrlDto(url));
    }

    public async Task<Result<bool>> ProcessWebhookAsync(
        string? secretFromQuery, string rawBody, string? signatureHeader)
    {
        if (!AbacateWebhook.IsAuthentic(_options, secretFromQuery, rawBody, signatureHeader))
            return Result<bool>.Fail(ErrorCodes.InvalidSignature, "Assinatura do webhook inválida.");

        var webhook = AbacateWebhook.Parse(rawBody);
        if (webhook is null)
            return Result<bool>.Ok(true);

        if (await db.ProcessedWebhookEvents.AnyAsync(item =>
                item.Provider == WebhookProvider && item.EventId == webhook.Id))
            return Result<bool>.Ok(true);

        db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
        {
            Provider = WebhookProvider,
            EventId = webhook.Id,
        });

        var clinic = webhook.ClinicaId is { } clinicaId
            ? await db.Clinicas.FirstOrDefaultAsync(item => item.Id == clinicaId && !item.IsDeleted)
            : null;
        if (clinic is null || clinic.IsFounder)
        {
            await SaveWebhookAsync();
            return Result<bool>.Ok(true);
        }

        switch (webhook.Type)
        {
            case "subscription.completed":
                clinic.AbacateSubscriptionId = webhook.SubscriptionId ?? clinic.AbacateSubscriptionId;
                if (!string.IsNullOrWhiteSpace(webhook.CustomerId))
                    clinic.AbacateCustomerId = webhook.CustomerId;
                if (TryResolveProduct(webhook.ProductId, out var plan, out var cycle))
                    await plans.ApplyPlanAsync(clinic, plan, cycle);
                break;

            case "subscription.renewed":
                db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
                {
                    ClinicaId = clinic.Id,
                    Type = CommercialHistoryEventType.SubscriptionRenewed,
                    Description = "Assinatura renovada.",
                });
                break;

            case "subscription.plan_changed":
                if (TryResolveProduct(webhook.ProductId, out var changedPlan, out var changedCycle))
                    await plans.ApplyPlanAsync(clinic, changedPlan, changedCycle);
                break;

            case "subscription.cancelled":
                await plans.ApplyPlanAsync(clinic, ClinicPlan.Free, null);
                clinic.AbacateSubscriptionId = null;
                break;

            case "checkout.completed":
                if (!string.IsNullOrWhiteSpace(webhook.CustomerId))
                    clinic.AbacateCustomerId = webhook.CustomerId;
                await ApplyOneTimePurchaseAsync(clinic, webhook.Item);
                break;
        }

        await SaveWebhookAsync();
        return Result<bool>.Ok(true);
    }

    private async Task ApplyOneTimePurchaseAsync(Clinica clinic, string? item)
    {
        if (string.IsNullOrWhiteSpace(item))
            return;

        if (item == ExtraProfessionalItem)
        {
            clinic.ExtraProfessionals += 1;
            db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
            {
                ClinicaId = clinic.Id,
                Type = CommercialHistoryEventType.ExtraProfessionalPurchased,
                Description = "Vaga extra de profissional comprada.",
            });
            return;
        }

        if (!item.StartsWith("pack:", StringComparison.Ordinal))
            return;
        var pack = PlanMatrix.Packs.FirstOrDefault(candidate => candidate.Id == item["pack:".Length..]);
        if (pack is null)
            return;

        await usage.GrantExtraAsync(clinic.Id, pack.Metric, pack.Amount);
        db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
        {
            ClinicaId = clinic.Id,
            Type = CommercialHistoryEventType.UsagePackPurchased,
            Description = $"Pacote {pack.Id} adquirido (válido até o fim deste mês).",
        });
    }

    private async Task SaveWebhookAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Evento concorrente com o mesmo id: o índice único garante que só um efeito foi aplicado.
        }
    }

    private bool TryResolveProduct(string? productId, out ClinicPlan plan, out BillingCycle cycle)
    {
        plan = ClinicPlan.Free;
        cycle = BillingCycle.Monthly;
        if (string.IsNullOrWhiteSpace(productId))
            return false;

        foreach (var (key, value) in _options.Products)
        {
            if (value != productId)
                continue;
            var parts = key.Split('_');
            if (parts.Length == 2
                && Enum.TryParse<ClinicPlan>(parts[0], out plan)
                && Enum.TryParse<BillingCycle>(parts[1], out cycle))
                return true;
        }

        return false;
    }

    private Task<Clinica?> CurrentClinicAsync() =>
        db.Clinicas.FirstOrDefaultAsync(item => item.Id == usuario.ClinicaId && !item.IsDeleted);

    private async Task<AbacateCustomer> CustomerAsync(Clinica clinic)
    {
        var email = await db.Users.Where(item => item.Id == usuario.UserId)
            .Select(item => item.Email).SingleAsync();
        return new AbacateCustomer(
            string.IsNullOrWhiteSpace(clinic.NomeFantasia) ? clinic.Nome : clinic.NomeFantasia,
            email,
            NullIfEmpty(clinic.Telefone),
            NullIfEmpty(clinic.Cnpj),
            clinic.AbacateCustomerId);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
