using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MultiClinica.API.Common;
using MultiClinica.API.Models;

namespace MultiClinica.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var roleClaim = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;

        if (roleClaim is null
            || !Enum.TryParse<UserRole>(roleClaim, out var role)
            || !PermissionMatrix.Has(role, permission))
        {
            context.Result = new ObjectResult(new { message = "Você não tem permissão para executar esta ação." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
