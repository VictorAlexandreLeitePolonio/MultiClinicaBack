using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Authorization;

// Lê o plano do banco (não do JWT) para que upgrade/rebaixamento valha sem novo login.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireFeatureAttribute(string feature) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return; // [Authorize] responde 401
        var services = context.HttpContext.RequestServices;
        var usuario = services.GetRequiredService<IUsuarioLogadoService>();
        if (usuario.IsSuperAdmin) return;
        var plan = await services.GetRequiredService<AppDbContext>().Clinicas
            .Where(c => c.Id == usuario.ClinicaId)
            .Select(c => (ClinicPlan?)c.Plan)
            .SingleOrDefaultAsync();
        if (plan is { } current && PlanMatrix.Has(current, feature)) return;
        context.Result = PlanErrors.PlanRequired(feature);
    }
}
