namespace InventorySystem.Core.Entities;

/// <summary>
/// 在庫表データ項目
/// </summary>
public class InventoryListItem
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
    /// 商品名（仮実装：商品コード表示）
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// 荷印コード
    /// </summary>
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 荷印名
    /// </summary>
    public string ManualShippingMark { get; set; } = string.Empty;
    
    /// <summary>
    /// 等級コード
    /// </summary>
    public string GradeCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 等級名（仮実装：等級コード表示）
    /// </summary>
    public string GradeName { get; set; } = string.Empty;
    
    /// <summary>
    /// 階級コード
    /// </summary>
    public string ClassCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 階級名（仮実装：階級コード表示）
    /// </summary>
    public string ClassName { get; set; } = string.Empty;
    
    /// <summary>
    /// 在庫数量: ZZ,ZZ9.99-
    /// </summary>
    public decimal CurrentStockQuantity { get; set; }
    
    /// <summary>
    /// 在庫単価: ZZZ,ZZ9
    /// </summary>
    public decimal CurrentStockUnitPrice { get; set; }
    
    /// <summary>
    /// 在庫金額: ZZ,ZZZ,ZZ9-
    /// </summary>
    public decimal CurrentStockAmount { get; set; }
    
    /// <summary>
    /// 最終入荷日: (YY-MM-DD)
    /// </summary>
    public DateTime? LastReceiptDate { get; set; }
    
    /// <summary>
    /// 滞留マーク: !（11-20日）、!!（21-30日）、!!!（31日以上）
    /// </summary>
    public string StagnationMark { get; set; } = string.Empty;
    
    /// <summary>
    /// 前日在庫数量
    /// </summary>
    public decimal PreviousStockQuantity { get; set; }
    
    /// <summary>
    /// 前日在庫金額
    /// </summary>
    public decimal PreviousStockAmount { get; set; }
    
    /// <summary>
    /// 在庫キー
    /// </summary>
    public InventoryKey InventoryKey => new()
    {
        ProductCode = ProductCode,
        GradeCode = GradeCode,
        ClassCode = ClassCode,
        ShippingMarkCode = ShippingMarkCode,
        ManualShippingMark = ManualShippingMark
    };
    
    /// <summary>
    /// 印字対象かどうかを判定
    /// DailyStock（= CurrentStockQuantity）が0でないものを印字（マイナス在庫も印字）
    /// </summary>
    public bool ShouldBePrinted()
    {
        // DailyStock != 0 の商品を表示（マイナス在庫を含む）
        return CurrentStockQuantity != 0;
    }
    
    /// <summary>
    /// 滞留警告マークを計算
    /// </summary>
    public static string CalculateStagnationMark(DateTime reportDate, DateTime? lastReceiptDate)
    {
        if (!lastReceiptDate.HasValue) return "";
        
        var daysSinceLastReceipt = (reportDate - lastReceiptDate.Value).Days;
        
        return daysSinceLastReceipt switch
        {
            >= 31 => "!!!",
            >= 21 => "!!",
            >= 11 => "!",
            _ => ""
        };
    }
    
    /// <summary>
    /// 除外対象かどうかを判定
    /// 在庫表では除外データも使用する（アンマッチリストとは異なる）
    /// </summary>
    public bool ShouldBeExcluded()
    {
        return false; // 在庫表では除外しない
    }
}

/// <summary>
/// 在庫表小計
/// </summary>
public class InventoryListSubtotal
{
    public string ProductCode { get; set; } = string.Empty;
    public string SubtotalName => "＊　小　　計　＊";
    
    /// <summary>
    /// 小計在庫数量
    /// </summary>
    public decimal SubtotalQuantity { get; set; }
    
    /// <summary>
    /// 小計在庫金額
    /// </summary>
    public decimal SubtotalAmount { get; set; }
}

/// <summary>
/// 在庫表合計
/// </summary>
public class InventoryListTotal
{
    public string TotalName => "※　合　　計　※";
    
    /// <summary>
    /// 総計在庫数量
    /// </summary>
    public decimal GrandTotalQuantity { get; set; }
    
    /// <summary>
    /// 総計在庫金額
    /// </summary>
    public decimal GrandTotalAmount { get; set; }
}

/// <summary>
/// 担当者別在庫表データ
/// </summary>
public class InventoryListByStaff
{
    /// <summary>
    /// 担当者コード
    /// </summary>
    public string StaffCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 担当者名
    /// </summary>
    public string StaffName { get; set; } = string.Empty;
    
    /// <summary>
    /// 在庫データリスト
    /// </summary>
    public List<InventoryListItem> Items { get; set; } = new();
    
    /// <summary>
    /// 小計データリスト
    /// </summary>
    public List<InventoryListSubtotal> Subtotals { get; set; } = new();
    
    /// <summary>
    /// 担当者合計
    /// </summary>
    public InventoryListTotal Total { get; set; } = new();
}
