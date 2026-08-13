namespace MultiClinica.API.DTOs.Financial;

public sealed class BalanceLowStockProductDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrentQuantity { get; set; }
    public int MinimumQuantity { get; set; }
}
