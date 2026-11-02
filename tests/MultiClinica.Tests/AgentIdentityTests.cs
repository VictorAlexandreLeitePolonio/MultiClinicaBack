using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using MultiClinica.API.Services.Interfaces;
using Xunit;

namespace MultiClinica.Tests;

public class AgentIdentityTests
{
    [Fact]
    public async Task Services_resolve_tenant_from_agent_identity_without_http_context()
    {
        await using var app = new MultiClinicaFactory();
        var (clinicId, adminId) = await AgentSeed.ClinicWithAdminAsync(app);

        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AgentIdentity>().Set(clinicId, adminId, UserRole.Administrador);

        var usuario = scope.ServiceProvider.GetRequiredService<IUsuarioLogadoService>();
        Assert.Equal(clinicId, usuario.ClinicaId);
        Assert.Equal(adminId, usuario.UserId);
        Assert.Equal(UserRole.Administrador, usuario.Role);

        var professionals = await scope.ServiceProvider.GetRequiredService<IAppointmentService>().GetProfessionalsAsync();
        Assert.Contains(professionals.Value!, p => p.Id == adminId);
    }

    [Fact]
    public async Task Without_agent_identity_and_http_context_it_still_throws()
    {
        await using var app = new MultiClinicaFactory();
        using var scope = app.Services.CreateScope();
        var usuario = scope.ServiceProvider.GetRequiredService<IUsuarioLogadoService>();
        Assert.Throws<InvalidOperationException>(() => usuario.ClinicaId);
    }
}
