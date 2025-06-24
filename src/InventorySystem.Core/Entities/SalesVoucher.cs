namespace InventorySystem.Core.Entities;

/// <summary>
/// 売上伝票エンティティ
/// 販売大臣AXの売上伝票CSVデータを格納
/// </summary>
public class SalesVoucher
{
    /// <summary>
    /// ID（自動採番）
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// データセットID（取込単位の識別）
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;

    /// <summary>
    /// 伝票番号
    /// </summary>
    public string VoucherNumber { get; set; } = string.Empty;

    /// <summary>
    /// 伝票日付
    /// </summary>
    public DateTime VoucherDate { get; set; }

    /// <summary>
    /// ジョブ日付（汎用日付2）
    /// </summary>
    public DateTime JobDate { get; set; }

    /// <summary>
    /// 伝票種別コード (51:掛売上, 52:現金売上)
    /// </summary>
    public string VoucherType { get; set; } = string.Empty;

    /// <summary>
    /// 明細種別コード (1:商品, 2:返品, 3:値引, 4:その他, 18:諸経費)
    /// </summary>
    public string DetailType { get; set; } = string.Empty;

    /// <summary>
    /// 得意先コード
    /// </summary>
    public string? CustomerCode { get; set; }

    /// <summary>
    /// 得意先名
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// 商品コード
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// 商品名
    /// </summary>
    public string? ProductName { get; set; }

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

    /// <summary>
    /// 数量
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// 単価
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 金額
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// 売上単価（UnitPriceのエイリアス）
    /// </summary>
    public decimal SalesUnitPrice 
    { 
        get => UnitPrice; 
        set => UnitPrice = value; 
    }
    
    /// <summary>
    /// 売上金額（Amountのエイリアス）
    /// </summary>
    public decimal SalesAmount 
    { 
        get => Amount; 
        set => Amount = value; 
    }

    /// <summary>
    /// 商品分類1（担当者コード）
    /// </summary>
    public string? ProductCategory1 { get; set; }

    /// <summary>
    /// 商品分類2
    /// </summary>
    public string? ProductCategory2 { get; set; }

    /// <summary>
    /// 商品分類3
    /// </summary>
    public string? ProductCategory3 { get; set; }

    /// <summary>
    /// 粗利益（後で計算して更新）
    /// </summary>
    public decimal? GrossProfit { get; set; }

    /// <summary>
    /// 除外フラグ（アンマッチ処理時）
    /// </summary>
    public bool IsExcluded { get; set; }

    /// <summary>
    /// 除外理由
    /// </summary>
    public string? ExcludeReason { get; set; }

    /// <summary>
    /// 取込日時
    /// </summary>
    public DateTime ImportedAt { get; set; }

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 部門コード
    /// </summary>
    public string DepartmentCode { get; set; } = "DeptA";

    /// <summary>
    /// 在庫キーを取得（互換性プロパティ）
    /// </summary>
    public InventoryKey InventoryKey { get; set; } = new();
    
    /// <summary>
    /// 取引種別（互換性プロパティ）
    /// </summary>
    public string TransactionType => VoucherType;
    
    /// <summary>
    /// 伝票ID（リポジトリで使用）
    /// </summary>
    public string VoucherId { get; set; } = string.Empty;
    
    /// <summary>
    /// 行番号（リポジトリで使用）
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// 在庫キーを取得
    /// </summary>
    public InventoryKey GetInventoryKey()
    {
        InventoryKey = new InventoryKey
        {
            ProductCode = ProductCode,
            GradeCode = GradeCode,
            ClassCode = ClassCode,
            ShippingMarkCode = ShippingMarkCode,
            ShippingMarkName = ShippingMarkName
        };
        return InventoryKey;
    }

    /// <summary>
    /// 除外対象かどうかを判定
    /// アンマッチ・商品勘定処理での除外条件
    /// </summary>
    public bool ShouldBeExcluded()
    {
        // 荷印名の先頭4文字が「EXIT」「exit」
        if (ShippingMarkName.Length >= 4)
        {
            var prefix = ShippingMarkName.Substring(0, 4).ToUpper();
            if (prefix == "EXIT")
            {
                return true;
            }
        }

        // 荷印コードが「9900」「9910」「1353」
        if (ShippingMarkCode == "9900" || ShippingMarkCode == "9910" || ShippingMarkCode == "1353")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 荷印名による商品分類1の自動設定
    /// </summary>
    public void ApplySpecialProcessingRules()
    {
        if (ShippingMarkName.Length >= 4)
        {
            var prefix = ShippingMarkName.Substring(0, 4);
            
            if (prefix == "9aaa")
            {
                ProductCategory1 = "8";
            }
            else if (prefix == "1aaa" || prefix == "0999")
            {
                ProductCategory1 = "6";
            }
        }
    }
}

