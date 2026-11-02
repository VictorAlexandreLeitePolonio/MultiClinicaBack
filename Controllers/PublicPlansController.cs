using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.Models;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/public/plans")]
[AllowAnonymous]
public class PublicPlansController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var plans = Enum.GetValues<ClinicPlan>().Select(plan =>
        {
            var limits = PlanMatrix.For(plan);
            return new
            {
                plan = plan.ToString(),
                monthlyPrice = limits.MonthlyPrice,
                annualPrice = PlanMatrix.AnnualPrice(plan),
                includedProfessionals = limits.IncludedProfessionals,
                maxExtraProfessionals = limits.MaxExtraProfessionals,
                features = limits.Features.OrderBy(feature => feature).ToArray(),
                quotas = new Dictionary<string, int>
                {
                    [UsageMetric.WhatsAppConversations.ToString()] = limits.WhatsAppConversationsPerMonth,
                    [UsageMetric.TranscriptionMinutes.ToString()] = limits.TranscriptionMinutesPerMonth,
                },
                hubRank = limits.HubRank,
            };
        }).ToList();

        return Ok(new
        {
            plans,
            extraProfessionalPrice = PlanMatrix.ExtraProfessionalPrice,
            packs = PlanMatrix.Packs.Select(pack => new
            {
                id = pack.Id,
                plan = pack.Plan.ToString(),
                metric = pack.Metric.ToString(),
                amount = pack.Amount,
                price = pack.Price,
            }),
        });
    }
}
