using System.Reflection;

namespace MultiClinica.API.Common;

public static class Permissions
{
    public static class Paciente
    {
        public const string VisualizarStatus = "financeiro.paciente.visualizar_status";
    }

    public static class ClinicSettings
    {
        public const string View = "clinic.settings.view";
        public const string Update = "clinic.settings.update";
    }

    public static IReadOnlyCollection<string> All { get; } = typeof(Permissions)
        .GetNestedTypes()
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet();
}
