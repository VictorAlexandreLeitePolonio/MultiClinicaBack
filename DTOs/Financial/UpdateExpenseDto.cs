namespace ProjetoLP.API.DTOs.Financial;

// Dados recebidos ao atualizar um gasto existente.
public class UpdateExpenseDto
{
    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReferenceMonth { get; set; } = string.Empty;
}
