namespace ProjetoLP.API.Models;

// Representa um gasto manual inserido pela admin para o controle financeiro mensal.
public class Expense
{
    public int Id { get; set; }

    // Título do gasto (ex: "Aluguel", "Conta de Luz").
    public string Title { get; set; } = string.Empty;

    // Valor do gasto em decimal — correto para representar dinheiro.
    public decimal Value { get; set; }

    // Data em que o gasto foi pago.
    public DateTime PaymentDate { get; set; }

    // Descrição detalhada do gasto.
    public string Description { get; set; } = string.Empty;

    // Mês de referência — formato "YYYY-MM" (ex: "2026-03").
    // Liga este gasto ao balanço do mês correspondente.
    public string ReferenceMonth { get; set; } = string.Empty;

    // Preenchido automaticamente na criação.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
