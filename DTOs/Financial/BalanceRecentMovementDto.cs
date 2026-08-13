namespace MultiClinica.API.DTOs.Financial;

public sealed class BalanceRecentMovementDto
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty; // Payment | Stock
    public string Type { get; set; } = string.Empty;   // AppointmentPayment | ProductSale | ProductPurchase | ProductOutput | ProductLoss | InternalUse
    public string Description { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public int? Quantity { get; set; }
    public DateTime Date { get; set; }
}
