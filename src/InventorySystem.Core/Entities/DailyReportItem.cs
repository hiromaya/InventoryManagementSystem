namespace InventorySystem.Core.Entities;

/// <summary>
/// 商品日報データ項目
/// </summary>
public class DailyReportItem
{
    /// <summary>
    /// 商品コード
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 商品分類1（担当者コード）
    /// </summary>
    public string ProductCategory1 { get; set; } = string.Empty;
    
    /// <summary>
    /// 商品名
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// 等級コード
    /// </summary>
    public string GradeCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 階級コード
    /// </summary>
    public string ClassCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 荷印コード
    /// </summary>
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 荷印名
    /// </summary>
    public string ShippingMarkName { get; set; } = string.Empty;
    
    // === 日計項目（11項目） ===
    
    /// <summary>
    /// 1. 売上数量: ZZ,ZZ9.99-
    /// </summary>
    public decimal DailySalesQuantity { get; set; }
    
    /// <summary>
    /// 2. 売上金額: ZZ,ZZZ,ZZ9-（売上金額＋売上返品金額）
    /// </summary>
    public decimal DailySalesAmount { get; set; }
    
    /// <summary>
    /// 3. 仕入値引: ZZ,ZZZ,ZZ9-
    /// </summary>
    public decimal DailyPurchaseDiscount { get; set; }
    
    /// <summary>
    /// 4. 在庫調整: ZZ,ZZZ,ZZ9-
    /// </summary>
    public decimal DailyInventoryAdjustment { get; set; }
    
    /// <summary>
    /// 5. 加工費: Z,ZZZ,ZZ9-
    /// </summary>
    public decimal DailyProcessingCost { get; set; }
    
    /// <summary>
    /// 6. 振替: Z,ZZZ,ZZ9-
    /// </summary>
    public decimal DailyTransfer { get; set; }
    
    /// <summary>
    /// 7. 奨励金: Z,ZZZ,ZZ9-
    /// </summary>
    public decimal DailyIncentive { get; set; }
    
    /// <summary>
    /// 8. １粗利益: ZZ,ZZZ,ZZ9-
    /// </summary>
    public decimal DailyGrossProfit1 { get; set; }
    
    /// <summary>
    /// 9. １粗利率: ZZ9.99-%（粗利益÷売上金額×100、小数第3位四捨五入）
    /// </summary>
    public decimal DailyGrossProfitRate1 { get; set; }
    
    /// <summary>
    /// 10. ２粗利益: ZZ,ZZZ,ZZ9-（１粗利益－歩引額）
    /// </summary>
    public decimal DailyGrossProfit2 { get; set; }
    
    /// <summary>
    /// 11. ２粗利率: ZZ9.99-%
    /// </summary>
    public decimal DailyGrossProfitRate2 { get; set; }
    
    // === 月計項目（5項目）※仮実装 ===
    
    /// <summary>
    /// 1. 売上金額: ZZZ,ZZZ,ZZ9-
    /// </summary>
    public decimal MonthlySalesAmount { get; set; }
    
    /// <summary>
    /// 2. １粗利益: ZZ,ZZZ,ZZ9-
    /// </summary>
    public decimal MonthlyGrossProfit1 { get; set; }
    
    /// <summary>
    /// 3. １粗利率: ZZ9.99-%
    /// </summary>
    public decimal MonthlyGrossProfitRate1 { get; set; }
    
    /// <summary>
    /// 4. ２粗利益: ZZ,ZZZ,ZZ9▲-
    /// </summary>
    public decimal MonthlyGrossProfit2 { get; set; }
    
    /// <summary>
    /// 5. ２粗利率: ZZ9.99-%
    /// </summary>
    public decimal MonthlyGrossProfitRate2 { get; set; }
    
    /// <summary>
    /// 歩引額（２粗利益計算用）
    /// </summary>
    public decimal DailyDiscountAmount { get; set; }
    
    /// <summary>
    /// オール0明細かどうかを判定
    /// </summary>
    public bool IsAllZero()
    {
        return DailySalesQuantity == 0 && DailySalesAmount == 0 && 
               DailyPurchaseDiscount == 0 && DailyInventoryAdjustment == 0 &&
               DailyProcessingCost == 0 && DailyTransfer == 0 && DailyIncentive == 0 &&
               DailyGrossProfit1 == 0 && DailyGrossProfit2 == 0;
    }
    
    /// <summary>
    /// 粗利率を計算（0除算対策）
    /// </summary>
    public static decimal CalculateGrossProfitRate(decimal grossProfit, decimal salesAmount)
    {
        if (salesAmount == 0) return 0;
        
        var rate = (grossProfit / salesAmount) * 100;
        // 小数第3位四捨五入
        return Math.Round(rate, 2, MidpointRounding.AwayFromZero);
    }
    
    /// <summary>
    /// 月計データを仮設定（日計と同じ値）
    /// </summary>
    public void SetTemporaryMonthlyData()
    {
        MonthlySalesAmount = DailySalesAmount;
        MonthlyGrossProfit1 = DailyGrossProfit1;
        MonthlyGrossProfitRate1 = DailyGrossProfitRate1;
        MonthlyGrossProfit2 = DailyGrossProfit2;
        MonthlyGrossProfitRate2 = DailyGrossProfitRate2;
    }
}

/// <summary>
/// 商品日報大分類計
/// </summary>
public class DailyReportSubtotal
{
    public string ProductCategory1 { get; set; } = string.Empty;
    public string SubtotalName => "＊　大分類計　＊";
    
    // 日計合計値
    public decimal TotalDailySalesQuantity { get; set; }
    public decimal TotalDailySalesAmount { get; set; }
    public decimal TotalDailyPurchaseDiscount { get; set; }
    public decimal TotalDailyInventoryAdjustment { get; set; }
    public decimal TotalDailyProcessingCost { get; set; }
    public decimal TotalDailyTransfer { get; set; }
    public decimal TotalDailyIncentive { get; set; }
    public decimal TotalDailyGrossProfit1 { get; set; }
    public decimal TotalDailyGrossProfit2 { get; set; }
    
    // 月計合計値
    public decimal TotalMonthlySalesAmount { get; set; }
    public decimal TotalMonthlyGrossProfit1 { get; set; }
    public decimal TotalMonthlyGrossProfit2 { get; set; }
    
    // 粗利率（計算値）
    public decimal TotalDailyGrossProfitRate1 => 
        DailyReportItem.CalculateGrossProfitRate(TotalDailyGrossProfit1, TotalDailySalesAmount);
    public decimal TotalDailyGrossProfitRate2 => 
        DailyReportItem.CalculateGrossProfitRate(TotalDailyGrossProfit2, TotalDailySalesAmount);
    public decimal TotalMonthlyGrossProfitRate1 => 
        DailyReportItem.CalculateGrossProfitRate(TotalMonthlyGrossProfit1, TotalMonthlySalesAmount);
    public decimal TotalMonthlyGrossProfitRate2 => 
        DailyReportItem.CalculateGrossProfitRate(TotalMonthlyGrossProfit2, TotalMonthlySalesAmount);
}

/// <summary>
/// 商品日報合計
/// </summary>
public class DailyReportTotal
{
    public string TotalName => "※　合　　計　※";
    
    // 全体合計値
    public decimal GrandTotalDailySalesQuantity { get; set; }
    public decimal GrandTotalDailySalesAmount { get; set; }
    public decimal GrandTotalDailyPurchaseDiscount { get; set; }
    public decimal GrandTotalDailyInventoryAdjustment { get; set; }
    public decimal GrandTotalDailyProcessingCost { get; set; }
    public decimal GrandTotalDailyTransfer { get; set; }
    public decimal GrandTotalDailyIncentive { get; set; }
    public decimal GrandTotalDailyGrossProfit1 { get; set; }
    public decimal GrandTotalDailyGrossProfit2 { get; set; }
    
    public decimal GrandTotalMonthlySalesAmount { get; set; }
    public decimal GrandTotalMonthlyGrossProfit1 { get; set; }
    public decimal GrandTotalMonthlyGrossProfit2 { get; set; }
    
    // 粗利率（計算値）
    public decimal GrandTotalDailyGrossProfitRate1 => 
        DailyReportItem.CalculateGrossProfitRate(GrandTotalDailyGrossProfit1, GrandTotalDailySalesAmount);
    public decimal GrandTotalDailyGrossProfitRate2 => 
        DailyReportItem.CalculateGrossProfitRate(GrandTotalDailyGrossProfit2, GrandTotalDailySalesAmount);
    public decimal GrandTotalMonthlyGrossProfitRate1 => 
        DailyReportItem.CalculateGrossProfitRate(GrandTotalMonthlyGrossProfit1, GrandTotalMonthlySalesAmount);
    public decimal GrandTotalMonthlyGrossProfitRate2 => 
        DailyReportItem.CalculateGrossProfitRate(GrandTotalMonthlyGrossProfit2, GrandTotalMonthlySalesAmount);
}