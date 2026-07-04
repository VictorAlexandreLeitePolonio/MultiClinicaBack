using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using Xunit;

namespace MultiClinica.Tests;

public class RequirePermissionAttributeTests
{
    private static AuthorizationFilterContext ContextFor(string? role)
    {
        var httpContext = new DefaultHttpContext();
        if (role is not null)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    [Fact]
    public void OnAuthorization_RecepcaoSemPermissao_Retorna403()
    {
        var attr = new RequirePermissionAttribute(Permissions.ContasReceber.EstornarRecebimento);
        var ctx = ContextFor("Recepcao");

        attr.OnAuthorization(ctx);

        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_RecepcaoComPermissao_NaoBloqueia()
    {
        var attr = new RequirePermissionAttribute(Permissions.ContasReceber.RegistrarRecebimento);
        var ctx = ContextFor("Recepcao");

        attr.OnAuthorization(ctx);

        Assert.Null(ctx.Result);
    }

    [Fact]
    public void OnAuthorization_SemClaimRole_Retorna403()
    {
        var attr = new RequirePermissionAttribute(Permissions.Caixa.Abrir);
        var ctx = ContextFor(null);

        attr.OnAuthorization(ctx);

        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_SuperAdmin_NaoBloqueia()
    {
        var attr = new RequirePermissionAttribute(Permissions.Compras.Aprovar);
        var ctx = ContextFor("SuperAdmin");

        attr.OnAuthorization(ctx);

        Assert.Null(ctx.Result);
    }
}
