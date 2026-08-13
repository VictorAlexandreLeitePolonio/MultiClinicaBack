namespace MultiClinica.API.DTOs.Stock;

public sealed class CreateStockMovementRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
}
