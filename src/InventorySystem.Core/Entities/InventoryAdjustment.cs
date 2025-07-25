namespace InventorySystem.Core.Entities;

/// <summary>
/// 在庫調整エンティティ
/// 販売大臣AXの在庫調整CSVデータを格納
/// </summary>
public class InventoryAdjustment
{
    /// <summary>
    /// 伝票ID（データベース用）
    /// </summary>
    public string VoucherId { get; set; } = string.Empty;

    /// <summary>
    /// 行番号（データベース用）
    /// </summary>
    public int LineNumber { get; set; }

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
    /// 区分コード (0:消費税,1:ロス,2:不明,3:不明,4:振替,5:不明,6:調整) ※受注伝票用
    /// </summary>
    public int? CategoryCode { get; set; }

    /// <summary>
    /// 調整区分（CategoryCodeの文字列版）
    /// </summary>
    public string AdjustmentCategory 
    { 
        get => CategoryCode?.ToString() ?? "1"; 
        set => CategoryCode = int.TryParse(value, out var parsed) ? parsed : null; 
    }
    
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
    /// 作成日（データベース用）
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// 商品分類1（担当者コード）- ビジネスロジック用、DBには保存しない
    /// </summary>
    public string? ProductCategory1 { get; set; }

    /// <summary>
    /// 商品分類2 - ビジネスロジック用、DBには保存しない
    /// </summary>
    public string? ProductCategory2 { get; set; }

    /// <summary>
    /// 商品分類3 - ビジネスロジック用、DBには保存しない
    /// </summary>
    public string? ProductCategory3 { get; set; }

    /// <summary>
    /// 除外フラグ（アンマッチ処理時）- ビジネスロジック用、DBには保存しない
    /// </summary>
    public bool IsExcluded { get; set; }

    /// <summary>
    /// 除外理由 - ビジネスロジック用、DBには保存しない
    /// </summary>
    public string? ExcludeReason { get; set; }

    /// <summary>
    /// 取込日時 - ビジネスロジック用、DBには保存しない
    /// </summary>
    public DateTime ImportedAt { get; set; }

    /// <summary>
    /// 作成日時 - ビジネスロジック用、DBには保存しない
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新日時 - ビジネスロジック用、DBには保存しない
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 部門コード - ビジネスロジック用、DBには保存しない
    /// </summary>
    public string DepartmentCode { get; set; } = "DeptA";

    /// <summary>
    /// アクティブフラグ（DataSetとの整合性管理用）
    /// </summary>
    public bool IsActive { get; set; } = true;

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
                // 区分コードも設定（商品分類1に基づいて）
                CategoryCode = 8;
            }
            else if (prefix == "1aaa" || prefix == "0999")
            {
                ProductCategory1 = "6";
                // 区分コードも設定（商品分類1に基づいて）
                CategoryCode = 6;
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

    /// <summary>
    /// 区分コードに基づいて集計タイプを取得
    /// </summary>
    /// <returns>集計タイプ（在庫調整、加工、振替）</returns>
    public string GetAggregationType()
    {
        if (!CategoryCode.HasValue)
            return "在庫調整"; // デフォルト

        return CategoryCode.Value switch
        {
            0 => "在庫調整", // 消費税（伝票単位消費税）
            1 => "在庫調整", // ロス
            2 => "在庫調整", // 不明（要確認）
            3 => "在庫調整", // 不明（要確認）
            4 => "振替",     // 振替
            5 => "在庫調整", // 不明（要確認）
            6 => "在庫調整", // 調整
            _ => "在庫調整"  // その他
        };
    }
}

/// <summary>
/// 在庫調整区分の種類
/// </summary>
public enum AdjustmentType
{
    /// <summary>
    /// 消費税（伝票単位消費税）
    /// </summary>
    Tax = 0,
    
    /// <summary>
    /// ロス
    /// </summary>
    Loss = 1,
    
    /// <summary>
    /// 不明（要確認）
    /// </summary>
    Unknown2 = 2,
    
    /// <summary>
    /// 不明（要確認）
    /// </summary>
    Unknown3 = 3,
    
    /// <summary>
    /// 振替
    /// </summary>
    Transfer = 4,
    
    /// <summary>
    /// 不明（要確認）
    /// </summary>
    Unknown5 = 5,
    
    /// <summary>
    /// 調整
    /// </summary>
    Adjustment = 6
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