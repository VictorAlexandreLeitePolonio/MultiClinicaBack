using System.Reflection;

namespace MultiClinica.API.Common;

public static class Permissions
{
    public static class FormasPagamento
    {
        public const string Visualizar = "financeiro.formas_pagamento.visualizar";
        public const string Criar = "financeiro.formas_pagamento.criar";
        public const string Editar = "financeiro.formas_pagamento.editar";
        public const string Inativar = "financeiro.formas_pagamento.inativar";
    }

    public static class Categorias
    {
        public const string Visualizar = "financeiro.categorias.visualizar";
        public const string Criar = "financeiro.categorias.criar";
        public const string Editar = "financeiro.categorias.editar";
        public const string Inativar = "financeiro.categorias.inativar";
    }

    public static class ContasFinanceiras
    {
        public const string Visualizar = "financeiro.contas_financeiras.visualizar";
        public const string Criar = "financeiro.contas_financeiras.criar";
        public const string Editar = "financeiro.contas_financeiras.editar";
        public const string Inativar = "financeiro.contas_financeiras.inativar";
    }

    public static class Movimentacoes
    {
        public const string Visualizar = "financeiro.movimentacoes.visualizar";
        public const string CriarAjuste = "financeiro.movimentacoes.criar_ajuste";
        public const string Estornar = "financeiro.movimentacoes.estornar";
    }

    public static class ContasReceber
    {
        public const string Visualizar = "financeiro.contas_receber.visualizar";
        public const string Criar = "financeiro.contas_receber.criar";
        public const string Editar = "financeiro.contas_receber.editar";
        public const string Cancelar = "financeiro.contas_receber.cancelar";
        public const string RegistrarRecebimento = "financeiro.contas_receber.registrar_recebimento";
        public const string EstornarRecebimento = "financeiro.contas_receber.estornar_recebimento";
        public const string VisualizarInadimplencia = "financeiro.contas_receber.visualizar_inadimplencia";
        public const string Exportar = "financeiro.contas_receber.exportar";
    }

    public static class Caixa
    {
        public const string Visualizar = "financeiro.caixa.visualizar";
        public const string Abrir = "financeiro.caixa.abrir";
        public const string Fechar = "financeiro.caixa.fechar";
        public const string VisualizarResumo = "financeiro.caixa.visualizar_resumo";
        public const string VisualizarMovimentacoes = "financeiro.caixa.visualizar_movimentacoes";
        public const string Ajustar = "financeiro.caixa.ajustar";
        public const string Reabrir = "financeiro.caixa.reabrir";
        public const string Cancelar = "financeiro.caixa.cancelar";
        public const string Exportar = "financeiro.caixa.exportar";
    }

    public static class ContasPagar
    {
        public const string Visualizar = "financeiro.contas_pagar.visualizar";
        public const string Criar = "financeiro.contas_pagar.criar";
        public const string Editar = "financeiro.contas_pagar.editar";
        public const string Cancelar = "financeiro.contas_pagar.cancelar";
        public const string RegistrarPagamento = "financeiro.contas_pagar.registrar_pagamento";
        public const string EstornarPagamento = "financeiro.contas_pagar.estornar_pagamento";
        public const string Exportar = "financeiro.contas_pagar.exportar";
    }

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
        public const string GerarContaPagar = "compras.gerar_conta_pagar";
        public const string VisualizarValores = "compras.visualizar_valores";
        public const string Exportar = "compras.exportar";
    }

    public static class Relatorios
    {
        public const string Visualizar = "relatorios.financeiro.visualizar";
        public const string Exportar = "relatorios.financeiro.exportar";
        public const string Faturamento = "relatorios.financeiro.faturamento";
        public const string Despesas = "relatorios.financeiro.despesas";
        public const string Lucro = "relatorios.financeiro.lucro";
        public const string Caixa = "relatorios.financeiro.caixa";
        public const string Inadimplencia = "relatorios.financeiro.inadimplencia";
        public const string Estoque = "relatorios.financeiro.estoque";
        public const string Compras = "relatorios.financeiro.compras";
    }

    public static class Paciente
    {
        public const string VisualizarStatus = "financeiro.paciente.visualizar_status";
    }

    public static class Auditoria
    {
        public const string Visualizar = "financeiro.auditoria.visualizar";
    }

    public static IReadOnlyCollection<string> All { get; } = typeof(Permissions)
        .GetNestedTypes()
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet();
}
