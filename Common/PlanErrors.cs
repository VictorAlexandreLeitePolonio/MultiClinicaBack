using Microsoft.AspNetCore.Mvc;

namespace MultiClinica.API.Common;

public static class PlanErrors
{
    public static ObjectResult PlanRequired(string feature) => new(new
    {
        code = ErrorCodes.PlanRequired,
        feature,
        requiredPlan = PlanMatrix.CheapestWith(feature).ToString(),
        message = $"Este recurso está disponível a partir do plano {PlanMatrix.CheapestWith(feature)}.",
    }) { StatusCode = StatusCodes.Status402PaymentRequired };

    public static ObjectResult ProfessionalLimit(int limit) => new(new
    {
        code = ErrorCodes.ProfessionalLimit,
        limit,
        message = $"Seu plano permite {limit} profissional(is) ativo(s). Compre uma vaga extra ou faça upgrade.",
    }) { StatusCode = StatusCodes.Status402PaymentRequired };
}
