using MultiClinica.API.Models;

namespace MultiClinica.API.Common;

public static class PermissionMatrix
{
    private static readonly IReadOnlyDictionary<UserRole, HashSet<string>> Map = Build();

    public static bool Has(UserRole role, string permission) =>
        Map.TryGetValue(role, out var set) && set.Contains(permission);

    public static IReadOnlyList<string> PermissionsFor(UserRole role) =>
        Map.TryGetValue(role, out var set)
            ? set.OrderBy(p => p, StringComparer.Ordinal).ToArray()
            : [];

    private static IReadOnlyDictionary<UserRole, HashSet<string>> Build()
    {
        var todas = Permissions.All.ToHashSet();

        var recepcao = new HashSet<string>
        {
            Permissions.Produtos.Visualizar,
            Permissions.Produtos.VisualizarPrecoVenda,
            Permissions.Estoque.MovimentacoesVisualizar,
            Permissions.ClinicSettings.View,
        };

        var profissional = new HashSet<string>
        {
            Permissions.Paciente.VisualizarStatus,
            Permissions.ClinicSettings.View,
        };

        return new Dictionary<UserRole, HashSet<string>>
        {
            [UserRole.SuperAdmin] = todas,
            [UserRole.Administrador] = todas,
            [UserRole.Recepcao] = recepcao,
            [UserRole.Profissional] = profissional,
        };
    }
}
