namespace InventorySystem.Core.Models;

/// <summary>
/// 在庫マスタに保持されている月計値を商品コード単位で集計した結果。
/// </summary>
public class InventoryMonthlyTotals
{
    public decimal MonthlySalesAmount { get; set; }
    public decimal MonthlySalesReturnAmount { get; set; }
    public decimal MonthlyGrossProfit1 { get; set; }
    public decimal MonthlyGrossProfit2 { get; set; }
    public decimal MonthlyWalkingAmount { get; set; }
}
