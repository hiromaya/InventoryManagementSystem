namespace InventorySystem.Core.ValueObjects;

public class GrossProfitCalculation
{
    public decimal DailyGrossProfit { get; }
    public decimal DailyAdjustmentAmount { get; }
    public decimal DailyProcessingCost { get; }
    public decimal FinalGrossProfit { get; }
    
    public GrossProfitCalculation(
        decimal dailyGrossProfit, 
        decimal dailyAdjustmentAmount, 
        decimal dailyProcessingCost)
    {
        DailyGrossProfit = dailyGrossProfit;
        DailyAdjustmentAmount = dailyAdjustmentAmount;
        DailyProcessingCost = dailyProcessingCost;
        
        // 第2段階：調整
        FinalGrossProfit = dailyGrossProfit - dailyAdjustmentAmount - dailyProcessingCost;
    }
    
    public static GrossProfitCalculation Zero => new(0, 0, 0);
}