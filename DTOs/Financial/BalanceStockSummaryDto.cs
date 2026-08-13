namespace MultiClinica.API.DTOs.Financial;

public sealed class BalanceStockSummaryDto
{
    public int TotalProducts { get; set; }
    public int ProductsBelowMinimum { get; set; }

    public int StockEntriesInPeriod { get; set; }
    public int StockOutputsInPeriod { get; set; }
    public int ProductSalesInPeriod { get; set; }
    public int ProductPurchasesInPeriod { get; set; }
    public int ProductLossesInPeriod { get; set; }
    public int ProductInternalUseInPeriod { get; set; }

    public List<BalanceLowStockProductDto> LowStockProducts { get; set; } = [];
}
