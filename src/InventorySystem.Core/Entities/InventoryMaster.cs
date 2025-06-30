namespace InventorySystem.Core.Entities;

public class InventoryMaster
{
    public InventoryKey Key { get; set; } = new();
    
    // 基本情報
    public string ProductName { get; set; } = string.Empty;      // 商品名
    public string Unit { get; set; } = string.Empty;             // 単位
    public decimal StandardPrice { get; set; }                   // 標準単価
    public string ProductCategory1 { get; set; } = string.Empty; // 商品分類1
    public string ProductCategory2 { get; set; } = string.Empty; // 商品分類2
    
    // 日付管理
    public DateTime JobDate { get; set; }                        // 汎用日付2（ジョブデート）
    public DateTime CreatedDate { get; set; }                    // 作成日
    public DateTime UpdatedDate { get; set; }                    // 更新日
    
    // 在庫情報
    public decimal CurrentStock { get; set; }                    // 現在在庫数
    public decimal CurrentStockAmount { get; set; }              // 現在在庫金額
    public decimal DailyStock { get; set; }                      // 当日在庫数
    public decimal DailyStockAmount { get; set; }                // 当日在庫金額
    public decimal PreviousMonthQuantity { get; set; }           // 前月末在庫数量
    public decimal PreviousMonthAmount { get; set; }             // 前月末在庫金額
    
    // 当日発生フラグ ('0':データあり, '9':クリア状態)
    public char DailyFlag { get; set; } = '9';
    
    // 粗利情報
    public decimal DailyGrossProfit { get; set; }                // 当日粗利益
    public decimal DailyAdjustmentAmount { get; set; }           // 当日在庫調整金額
    public decimal DailyProcessingCost { get; set; }             // 当日加工費
    public decimal FinalGrossProfit { get; set; }                // 最終粗利益
    
    // データセットID管理
    public string DataSetId { get; set; } = string.Empty;       // データセットID
    
    public bool IsExcluded
    {
        get
        {
            // アンマッチ・商品勘定でのみ除外
            var markName = Key.ShippingMarkName?.ToUpper() ?? string.Empty;
            var markCode = Key.ShippingMarkCode ?? string.Empty;
            
            return markName.StartsWith("EXIT") || 
                   markCode == "9900" || 
                   markCode == "9910" || 
                   markCode == "1353";
        }
    }
    
    public string GetAdjustedProductCategory1()
    {
        var markName = Key.ShippingMarkName ?? string.Empty;
        
        // 特殊処理ルール
        if (markName.StartsWith("9aaa")) return "8";
        if (markName.StartsWith("1aaa")) return "6";
        if (markName.StartsWith("0999")) return "6";
        
        return ProductCategory1;
    }
}