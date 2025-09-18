namespace InventorySystem.Core.Entities;

public class InventoryMaster
{
    public InventoryKey Key { get; set; } = new();
    
    // 基本情報
    public string ProductName { get; set; } = string.Empty;      // 商品名
    public string Unit { get; set; } = string.Empty;             // 単位
    public decimal StandardPrice { get; set; }                   // 標準単価
    public decimal AveragePrice { get; set; }                    // 平均単価（粗利計算用）
    public int PersonInChargeCode { get; set; }                  // 商品分類１担当者コード
    public string ProductCategory1 { get; set; } = string.Empty; // 商品分類1
    public string ProductCategory2 { get; set; } = string.Empty; // 商品分類2
    
    // 日付管理
    public DateTime JobDate { get; set; }                        // 汎用日付2（ジョブデート）
    public DateTime CreatedDate { get; set; }                    // 作成日
    public DateTime UpdatedDate { get; set; }                    // 更新日
    public DateTime? LastSalesDate { get; set; }                 // 最終売上日
    public DateTime? LastPurchaseDate { get; set; }              // 最終仕入日
    
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
    public decimal DailyWalkingAmount { get; set; }              // 日計歩引額

    // 月計情報（商品日報用）
    public decimal MonthlySalesAmount { get; set; }               // 月計売上金額
    public decimal MonthlySalesReturnAmount { get; set; }         // 月計売上返品金額
    public decimal MonthlyGrossProfit1 { get; set; }              // 月計1粗利益
    public decimal MonthlyGrossProfit2 { get; set; }              // 月計2粗利益（歩引後）
    public decimal MonthlyWalkingAmount { get; set; }             // 月計歩引額
    
    // データセットID管理
    public string DataSetId { get; set; } = string.Empty;       // データセットID
    
    // 世代管理用の新規フィールド
    public bool IsActive { get; set; } = true;                  // アクティブフラグ
    public string? ParentDataSetId { get; set; }                // 親データセットID
    public string ImportType { get; set; } = "UNKNOWN";         // インポートタイプ (INIT/IMPORT/CARRYOVER/MANUAL/UNKNOWN)
    public string? CreatedBy { get; set; }                      // 作成者
    public DateTime CreatedAt { get; set; } = DateTime.Now;     // 作成日時（CreatedDateとは別管理）
    public DateTime? UpdatedAt { get; set; }                    // 更新日時
    
    public bool IsExcluded
    {
        get
        {
            // アンマッチ・商品勘定でのみ除外
            var markName = Key.ManualShippingMark?.ToUpper() ?? string.Empty;
            var markCode = Key.ShippingMarkCode ?? string.Empty;
            
            return markName.StartsWith("EXIT") || 
                   markCode == "9900" || 
                   markCode == "9910" || 
                   markCode == "1353";
        }
    }
    
    public string GetAdjustedProductCategory1()
    {
        var markName = Key.ManualShippingMark ?? string.Empty;
        
        // 特殊処理ルール
        if (markName.StartsWith("9aaa")) return "8";
        if (markName.StartsWith("1aaa")) return "6";
        if (markName.StartsWith("0999")) return "6";
        
        return ProductCategory1;
    }
}
