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

        Assert.Contains("financeiro.contas_receber.registrar_recebimento", all);
        Assert.True(all.Count > 50);
    }

    [Fact]
    public void PermissionsFor_SuperAdmin_RetornaCatalogoCompleto()
    {
        var perms = PermissionMatrix.PermissionsFor(UserRole.SuperAdmin);

        Assert.Equal(Permissions.All.Count, perms.Count);
    }

    [Fact]
    public void Has_RecepcaoRegistrarRecebimento_RetornaVerdadeiro()
    {
        Assert.True(PermissionMatrix.Has(
            UserRole.Recepcao,
            Permissions.ContasReceber.RegistrarRecebimento));
    }

    [Fact]
    public void Has_RecepcaoEstornoRecebimento_RetornaFalso()
    {
        Assert.False(PermissionMatrix.Has(
            UserRole.Recepcao,
            Permissions.ContasReceber.EstornarRecebimento));
    }

    [Fact]
    public void Has_RecepcaoVisualizarCustoProduto_RetornaFalso()
    {
        Assert.False(PermissionMatrix.Has(
            UserRole.Recepcao,
            Permissions.Produtos.VisualizarCusto));
    }

    [Fact]
    public void PermissionsFor_Recepcao_ContemExatamente14Permissoes()
    {
        var perms = PermissionMatrix.PermissionsFor(UserRole.Recepcao);

        Assert.Equal(14, perms.Count);
    }

    [Fact]
    public void PermissionsFor_Profissional_ContemApenasStatusPaciente()
    {
        var perms = PermissionMatrix.PermissionsFor(UserRole.Profissional);

        Assert.Single(perms);
        Assert.Contains(Permissions.Paciente.VisualizarStatus, perms);
    }
}
