namespace InventorySystem.Core.Entities;

public class CpInventoryMaster
{
    public InventoryKey Key { get; set; } = new();
    
    // 基本情報
    public string ProductName { get; set; } = string.Empty;      // 商品名
    public string Unit { get; set; } = string.Empty;             // 単位
    public decimal StandardPrice { get; set; }                   // 標準単価
    public string ProductCategory1 { get; set; } = string.Empty; // 商品分類1
    public string ProductCategory2 { get; set; } = string.Empty; // 商品分類2
    
    // マスタ参照情報（商品勘定・在庫表で使用）
    public string GradeName { get; set; } = string.Empty;        // 等級名
    public string ClassName { get; set; } = string.Empty;        // 階級名
    public string ShippingMarkName { get; set; } = string.Empty; // 荷印名（表示用）
    public string ManualShippingMark { get; set; } = string.Empty; // 手入力荷印（8文字固定）
    
    // 日付管理
    public DateTime JobDate { get; set; }                        // 汎用日付2（ジョブデート）
    public DateTime CreatedDate { get; set; }                    // 作成日
    public DateTime UpdatedDate { get; set; }                    // 更新日
    
    // 前日在庫情報
    public decimal PreviousDayStock { get; set; }                // 前日在庫数
    public decimal PreviousDayStockAmount { get; set; }          // 前日在庫金額
    public decimal PreviousDayUnitPrice { get; set; }            // 前日在庫単価
    
    // 当日在庫情報
    public decimal DailyStock { get; set; }                      // 当日在庫数
    public decimal DailyStockAmount { get; set; }                // 当日在庫金額
    public decimal DailyUnitPrice { get; set; }                  // 当日在庫単価
    public decimal AveragePrice { get; set; }                    // 平均単価（粗利計算用）
    
    // 当日発生フラグ ('0':処理済み, '9':未処理)
    public char DailyFlag { get; set; } = '9';
    
    // 当日売上関連
    public decimal DailySalesQuantity { get; set; }              // 当日売上数量
    public decimal DailySalesAmount { get; set; }                // 当日売上金額
    public decimal DailySalesReturnQuantity { get; set; }        // 当日売上返品数量  
    public decimal DailySalesReturnAmount { get; set; }          // 当日売上返品金額
    
    // 当日仕入関連
    public decimal DailyPurchaseQuantity { get; set; }           // 当日仕入数量
    public decimal DailyPurchaseAmount { get; set; }             // 当日仕入金額
    public decimal DailyPurchaseReturnQuantity { get; set; }     // 当日仕入返品数量
    public decimal DailyPurchaseReturnAmount { get; set; }       // 当日仕入返品金額
    
    // 当日在庫調整関連
    public decimal DailyInventoryAdjustmentQuantity { get; set; }// 当日在庫調整数量
    public decimal DailyInventoryAdjustmentAmount { get; set; }  // 当日在庫調整金額
    
    // 当日加工・振替関連
    public decimal DailyProcessingQuantity { get; set; }         // 当日加工数量
    public decimal DailyProcessingAmount { get; set; }           // 当日加工金額
    public decimal DailyTransferQuantity { get; set; }           // 当日振替数量
    public decimal DailyTransferAmount { get; set; }             // 当日振替金額
    
    // 当日出入荷関連
    public decimal DailyReceiptQuantity { get; set; }            // 当日入荷数量
    public decimal DailyReceiptAmount { get; set; }              // 当日入荷金額
    public decimal DailyShipmentQuantity { get; set; }           // 当日出荷数量
    public decimal DailyShipmentAmount { get; set; }             // 当日出荷金額
    
    // 粗利関連
    public decimal DailyGrossProfit { get; set; }                // 当日粗利益
    public decimal DailyWalkingAmount { get; set; }              // 当日歩引き額
    public decimal DailyIncentiveAmount { get; set; }            // 当日奨励金
    public decimal DailyDiscountAmount { get; set; }             // 当日歩引き額
    public decimal DailyPurchaseDiscountAmount { get; set; }     // 当日仕入値引き額
    
    // 月計項目（月初から当日までの累計）
    public decimal MonthlySalesQuantity { get; set; }           // 月計売上数量
    public decimal MonthlySalesAmount { get; set; }             // 月計売上金額
    public decimal MonthlySalesReturnQuantity { get; set; }     // 月計売上返品数量
    public decimal MonthlySalesReturnAmount { get; set; }       // 月計売上返品金額
    public decimal MonthlyPurchaseQuantity { get; set; }        // 月計仕入数量
    public decimal MonthlyPurchaseAmount { get; set; }          // 月計仕入金額
    public decimal MonthlyPurchaseReturnQuantity { get; set; }  // 月計仕入返品数量
    public decimal MonthlyPurchaseReturnAmount { get; set; }    // 月計仕入返品金額
    public decimal MonthlyInventoryAdjustmentQuantity { get; set; } // 月計在庫調整数量
    public decimal MonthlyInventoryAdjustmentAmount { get; set; } // 月計在庫調整金額
    public decimal MonthlyProcessingQuantity { get; set; }      // 月計加工数量
    public decimal MonthlyProcessingAmount { get; set; }        // 月計加工金額
    public decimal MonthlyTransferQuantity { get; set; }        // 月計振替数量
    public decimal MonthlyTransferAmount { get; set; }          // 月計振替金額
    public decimal MonthlyGrossProfit { get; set; }             // 月計粗利益
    public decimal MonthlyWalkingAmount { get; set; }           // 月計歩引き額
    public decimal MonthlyIncentiveAmount { get; set; }         // 月計奨励金

    // 最終入荷日（仕入・在庫調整(1/3/6)・振替(4)のプラス数量で更新）
    public DateTime? LastReceiptDate { get; set; }

    // DataSetId管理を廃止（仮テーブル設計のため）
    
    // 部門コード
    public string DepartmentCode { get; set; } = "DeptA";
    
    /// <summary>
    /// 当日在庫数量を計算する
    /// </summary>
    public void CalculateDailyStock()
    {
        DailyStock = PreviousDayStock + DailyPurchaseQuantity + DailyInventoryAdjustmentQuantity - DailySalesQuantity;
    }
    
    /// <summary>
    /// 除外データかどうかを判定する（アンマッチ・商品勘定でのみ使用）
    /// </summary>
    public bool IsExcluded
    {
        get
        {
            var markName = Key.ManualShippingMark?.ToUpper() ?? string.Empty;
            var markCode = Key.ShippingMarkCode ?? string.Empty;
            
            return markName.StartsWith("EXIT") || 
                   markCode == "9900" || 
                   markCode == "9910" || 
                   markCode == "1353";
        }
    }
    
    /// <summary>
    /// 商品分類1を特殊ルールに従って調整する
    /// </summary>
    public string GetAdjustedProductCategory1()
    {
        var markName = Key.ManualShippingMark ?? string.Empty;
        
        if (markName.StartsWith("9aaa")) return "8";
        if (markName.StartsWith("1aaa")) return "6";
        if (markName.StartsWith("0999")) return "6";
        
        return ProductCategory1;
    }
    
    /// <summary>
    /// 当日エリアをクリアし、当日発生フラグを'9'にセットする
    /// </summary>
    public void ClearDailyArea()
    {
        // 当日売上関連をクリア
        DailySalesQuantity = 0;
        DailySalesAmount = 0;
        DailySalesReturnQuantity = 0;
        DailySalesReturnAmount = 0;
        
        // 当日仕入関連をクリア
        DailyPurchaseQuantity = 0;
        DailyPurchaseAmount = 0;
        DailyPurchaseReturnQuantity = 0;
        DailyPurchaseReturnAmount = 0;
        
        // 当日在庫調整関連をクリア
        DailyInventoryAdjustmentQuantity = 0;
        DailyInventoryAdjustmentAmount = 0;
        
        // 当日加工・振替関連をクリア
        DailyProcessingQuantity = 0;
        DailyProcessingAmount = 0;
        DailyTransferQuantity = 0;
        DailyTransferAmount = 0;
        
        // 当日出入荷関連をクリア
        DailyReceiptQuantity = 0;
        DailyReceiptAmount = 0;
        DailyShipmentQuantity = 0;
        DailyShipmentAmount = 0;
        
        // 粗利関連をクリア
        DailyGrossProfit = 0;
        DailyWalkingAmount = 0;
        DailyIncentiveAmount = 0;
        DailyDiscountAmount = 0;
        DailyPurchaseDiscountAmount = 0;
        
        // 当日在庫をクリア
        DailyStock = 0;
        DailyStockAmount = 0;
        DailyUnitPrice = 0;
        
        // 当日発生フラグを未処理にセット
        DailyFlag = '9';
        
        // 最新更新日を設定
        UpdatedDate = DateTime.Now;
    }
}
