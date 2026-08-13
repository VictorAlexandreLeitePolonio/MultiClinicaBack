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

        Assert.Contains(Permissions.Compras.Aprovar, all);
        Assert.True(all.Count > 20);
    }

    [Fact]
    public void PermissionsFor_SuperAdmin_RetornaCatalogoCompleto()
    {
        var perms = PermissionMatrix.PermissionsFor(UserRole.SuperAdmin);

        Assert.Equal(Permissions.All.Count, perms.Count);
    }

    [Fact]
    public void Has_RecepcaoMovimentacoesVisualizar_RetornaVerdadeiro()
    {
        Assert.True(PermissionMatrix.Has(
            UserRole.Recepcao,
            Permissions.Estoque.MovimentacoesVisualizar));
    }

    [Fact]
    public void Has_RecepcaoAprovarCompra_RetornaFalso()
    {
        Assert.False(PermissionMatrix.Has(
            UserRole.Recepcao,
            Permissions.Compras.Aprovar));
    }

    [Fact]
    public void Has_RecepcaoVisualizarCustoProduto_RetornaFalso()
    {
        Assert.False(PermissionMatrix.Has(
            UserRole.Recepcao,
            Permissions.Produtos.VisualizarCusto));
    }

    [Fact]
    public void PermissionsFor_Recepcao_ContemExatamente3Permissoes()
    {
        var perms = PermissionMatrix.PermissionsFor(UserRole.Recepcao);

        Assert.Equal(3, perms.Count);
        Assert.Contains(Permissions.Estoque.MovimentacoesVisualizar, perms);
    }

    [Fact]
    public void PermissionsFor_Profissional_ContemApenasStatusPaciente()
    {
        var perms = PermissionMatrix.PermissionsFor(UserRole.Profissional);

        Assert.Single(perms);
        Assert.Contains(Permissions.Paciente.VisualizarStatus, perms);
    }
}
