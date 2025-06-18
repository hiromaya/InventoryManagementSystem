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
    /// 在庫キーを取得（互換性プロパティ）
    /// </summary>
    public InventoryKey InventoryKey => GetInventoryKey();
    
    /// <summary>
    /// 取引種別（互換性プロパティ）
    /// </summary>
    public string TransactionType => VoucherType;

    /// <summary>
    /// 在庫キーを取得
    /// </summary>
    public InventoryKey GetInventoryKey()
    {
        return new InventoryKey
        {
            ProductCode = ProductCode,
            GradeCode = GradeCode,
            ClassCode = ClassCode,
            ShippingMarkCode = ShippingMarkCode,
            ShippingMarkName = ShippingMarkName
        };
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

/// <summary>
/// 売上伝票種別の定数
/// </summary>
public static class SalesVoucherTypes
{
    /// <summary>
    /// 掛売上
    /// </summary>
    public const string Credit = "51";

    /// <summary>
    /// 現金売上
    /// </summary>
    public const string Cash = "52";
}

/// <summary>
/// 明細種別の定数
/// </summary>
public static class DetailTypes
{
    /// <summary>
    /// 商品
    /// </summary>
    public const string Product = "1";

    /// <summary>
    /// 返品
    /// </summary>
    public const string Return = "2";

    /// <summary>
    /// 値引
    /// </summary>
    public const string Discount = "3";

    /// <summary>
    /// その他
    /// </summary>
    public const string Other = "4";

    /// <summary>
    /// 諸経費
    /// </summary>
    public const string Expense = "18";
}