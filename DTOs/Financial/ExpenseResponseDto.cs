namespace ProjetoLP.API.DTOs.Financial;

// Dados retornados ao cliente nas respostas da API para gastos.
public class ExpenseResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReferenceMonth { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
