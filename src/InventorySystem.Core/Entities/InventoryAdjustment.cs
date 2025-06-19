namespace InventorySystem.Core.Entities;

/// <summary>
/// 在庫調整エンティティ
/// 販売大臣AXの在庫調整CSVデータを格納
/// </summary>
public class InventoryAdjustment
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
    /// 伝票種別コード (71,72) ※無視される
    /// </summary>
    public string VoucherType { get; set; } = string.Empty;

    /// <summary>
    /// 明細種別コード ※無視される
    /// </summary>
    public string DetailType { get; set; } = string.Empty;

    /// <summary>
    /// 単位コード (01-06) ※重要：これで取込判定
    /// </summary>
    public string UnitCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 区分コード (1:ロス,4:振替,6:調整) ※受注伝票用
    /// </summary>
    public int? CategoryCode { get; set; }
    
    /// <summary>
    /// 得意先コード（受注伝票用）
    /// </summary>
    public string? CustomerCode { get; set; }
    
    /// <summary>
    /// 得意先名（受注伝票用）
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

    /// <summary>
    /// 取込対象の単位コードかどうかを判定
    /// </summary>
    public bool IsValidUnitCode()
    {
        return UnitCode == "01" || UnitCode == "02" || UnitCode == "03" ||
               UnitCode == "04" || UnitCode == "05" || UnitCode == "06";
    }
}

/// <summary>
/// 在庫調整用単位コードの定数
/// </summary>
public static class InventoryAdjustmentUnitCodes
{
    public const string Unit01 = "01";
    public const string Unit02 = "02";
    public const string Unit03 = "03";
    public const string Unit04 = "04";
    public const string Unit05 = "05";
    public const string Unit06 = "06";

    /// <summary>
    /// 有効な単位コードの配列
    /// </summary>
    public static readonly string[] ValidCodes = { Unit01, Unit02, Unit03, Unit04, Unit05, Unit06 };
}