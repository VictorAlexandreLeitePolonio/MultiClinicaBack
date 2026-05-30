namespace ProjetoLP.API.DTOs.Financial;

// Dados recebidos ao criar um novo gasto.
public class CreateExpenseDto
{
    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Description { get; set; } = string.Empty;

    // Formato "YYYY-MM" — determina a qual balanço mensal esse gasto pertence.
    public string ReferenceMonth { get; set; } = string.Empty;
}
