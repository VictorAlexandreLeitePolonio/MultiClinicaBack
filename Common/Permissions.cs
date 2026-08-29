using System.Reflection;

namespace MultiClinica.API.Common;

public static class Permissions
{
    public static class Fornecedores
    {
        public const string Visualizar = "financeiro.fornecedores.visualizar";
        public const string Criar = "financeiro.fornecedores.criar";
        public const string Editar = "financeiro.fornecedores.editar";
        public const string Inativar = "financeiro.fornecedores.inativar";
        public const string VisualizarHistoricoCompras = "financeiro.fornecedores.visualizar_historico_compras";
    }

    public static class Produtos
    {
        public const string Visualizar = "estoque.produtos.visualizar";
        public const string Criar = "estoque.produtos.criar";
        public const string Editar = "estoque.produtos.editar";
        public const string Inativar = "estoque.produtos.inativar";
        public const string VisualizarCusto = "estoque.produtos.visualizar_custo";
        public const string VisualizarPrecoVenda = "estoque.produtos.visualizar_preco_venda";
    }

    public static class Estoque
    {
        public const string MovimentacoesVisualizar = "estoque.movimentacoes.visualizar";
        public const string Entrada = "estoque.movimentacoes.entrada";
        public const string Saida = "estoque.movimentacoes.saida";
        public const string Ajuste = "estoque.movimentacoes.ajuste";
        public const string Perda = "estoque.movimentacoes.perda";
        public const string CancelarMovimentacao = "estoque.movimentacoes.cancelar";
        public const string AlertasVisualizar = "estoque.alertas.visualizar";
        public const string InventarioVisualizar = "estoque.inventario.visualizar";
        public const string InventarioRealizar = "estoque.inventario.realizar";
    }

    public static class Compras
    {
        public const string Visualizar = "compras.visualizar";
        public const string Criar = "compras.criar";
        public const string Editar = "compras.editar";
        public const string Cancelar = "compras.cancelar";
        public const string Aprovar = "compras.aprovar";
        public const string ReceberProdutos = "compras.receber_produtos";
        public const string VisualizarValores = "compras.visualizar_valores";
        public const string Exportar = "compras.exportar";
    }

    public static class Paciente
    {
        public const string VisualizarStatus = "financeiro.paciente.visualizar_status";
    }

    public static class Auditoria
    {
        public const string Visualizar = "financeiro.auditoria.visualizar";
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
