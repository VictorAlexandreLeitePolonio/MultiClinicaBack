namespace ProjetoLP.API.DTOs.Financial;

// Balanço financeiro mensal — calculado dinamicamente a partir dos gastos e pagamentos do mês.
public class FinancialBalanceDto
{
    // Mês de referência no formato "YYYY-MM".
    public string ReferenceMonth { get; set; } = string.Empty;

    // Soma de todos os gastos inseridos para este mês.
    public decimal TotalExpenses { get; set; }

    // Soma dos payments com status "Paid" para este mês.
    public decimal TotalIncome { get; set; }

    // Saldo líquido: TotalIncome - TotalExpenses.
    // Positivo = lucro, Negativo = prejuízo.
    public decimal NetBalance { get; set; }
}
