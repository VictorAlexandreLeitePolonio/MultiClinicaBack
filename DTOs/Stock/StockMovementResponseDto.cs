namespace MultiClinica.API.DTOs.Stock;

public sealed class StockMovementResponseDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int PreviousQuantity { get; set; }
    public int CurrentQuantity { get; set; }
    public decimal? UnitValue { get; set; }
    public decimal? TotalValue { get; set; }
    public string? Source { get; set; }
    public int? SourceId { get; set; }
    public string? Note { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
}
