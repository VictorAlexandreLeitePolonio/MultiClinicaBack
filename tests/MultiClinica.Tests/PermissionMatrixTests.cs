using MultiClinica.API.Common;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PermissionMatrixTests
{
    [Fact]
    public void All_Catalogo_ContemPermissaoConhecida()
    {
        var all = Permissions.All;

        Assert.Contains(Permissions.ClinicSettings.Update, all);
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void PermissionsFor_SuperAdmin_RetornaCatalogoCompleto()
    {
        var perms = PermissionMatrix.PermissionsFor(UserRole.SuperAdmin);

        Assert.Equal(Permissions.All.Count, perms.Count);
    }

    [Fact]
    public void Has_RecepcaoClinicSettingsView_RetornaVerdadeiro()
    {
        Assert.True(PermissionMatrix.Has(
            UserRole.Recepcao,
            Permissions.ClinicSettings.View));
    }

    [Fact]
    public void Has_RecepcaoVisualizarStatusPaciente_RetornaFalso()
    {
        Assert.False(PermissionMatrix.Has(
            UserRole.Recepcao,
            Permissions.Paciente.VisualizarStatus));
    }

    [Fact]
    public void Has_RecepcaoClinicSettingsUpdate_RetornaFalso()
    {
        Assert.False(PermissionMatrix.Has(
            UserRole.Recepcao,
            Permissions.ClinicSettings.Update));
    }

    [Fact]
    public void PermissionsFor_Recepcao_ContemExatamente1Permissao()
    {
        var perms = PermissionMatrix.PermissionsFor(UserRole.Recepcao);

        Assert.Single(perms);
        Assert.Contains(Permissions.ClinicSettings.View, perms);
    }

    [Fact]
    public void Has_AdministradorClinicSettingsUpdate_RetornaVerdadeiro()
    {
        Assert.True(PermissionMatrix.Has(
            UserRole.Administrador,
            Permissions.ClinicSettings.Update));
    }

    [Fact]
    public void Has_ProfissionalClinicSettingsUpdate_RetornaFalso()
    {
        Assert.False(PermissionMatrix.Has(
            UserRole.Profissional,
            Permissions.ClinicSettings.Update));
    }

    [Fact]
    public void PermissionsFor_Profissional_ContemStatusPacienteEClinicSettingsView()
    {
        var perms = PermissionMatrix.PermissionsFor(UserRole.Profissional);

        Assert.Equal(2, perms.Count);
        Assert.Contains(Permissions.Paciente.VisualizarStatus, perms);
        Assert.Contains(Permissions.ClinicSettings.View, perms);
    }
}
