using System.Globalization;
using CsvHelper.Configuration.Attributes;
using InventorySystem.Import.Validators;
using InventorySystem.Import.Helpers;

namespace InventorySystem.Import.Models;

/// <summary>
/// 前月末在庫CSVマッピングクラス（161列フォーマット）
/// 受注伝票と同じフォーマット、システム導入時の初期在庫設定用
/// </summary>
public class PreviousMonthInventoryCsv
{
    [Name("伝票日付")]
    [Index(0)]
    public string VoucherDate { get; set; } = string.Empty;
    
    [Name("伝票区分(71:在庫調整)")]
    [Index(1)]
    public string VoucherType { get; set; } = string.Empty;
    
    [Name("ジョブデート")]
    [Index(47)]  // 48列目
    public string JobDate { get; set; } = string.Empty;
    
    [Name("明細種(1固定)")]
    [Index(81)]  // 82列目
    public string DetailType { get; set; } = string.Empty;
    
    [Name("等級コード")]
    [Index(83)]  // 84列目
    public string GradeCode { get; set; } = string.Empty;
    
    [Name("階級コード")]
    [Index(84)]  // 85列目
    public string ClassCode { get; set; } = string.Empty;
    
    [Name("荷印コード")]
    [Index(85)]  // 86列目
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    [Name("商品コード")]
    [Index(89)]  // 90列目
    public string ProductCode { get; set; } = string.Empty;
    
    [Name("商品名")]
    [Index(145)]  // 146列目
    public string ProductName { get; set; } = string.Empty;
    
    [Name("荷印名")]
    [Index(141)]  // 142列目
    public string ManualShippingMark { get; set; } = string.Empty;
    
    [Name("数量")]
    [Index(94)]  // 95列目
    public string QuantityString { get; set; } = string.Empty;
    
    [Name("区分(1:ﾛｽ,4:振替,6:調整)")]
    [Index(95)]  // 96列目
    public string CategoryCode { get; set; } = string.Empty;
    
    [Name("単価")]
    [Index(96)]  // 97列目
    public string UnitPriceString { get; set; } = string.Empty;
    
    [Name("金額")]
    [Index(97)]  // 98列目
    public string AmountString { get; set; } = string.Empty;
    
    /// <summary>
    /// 数量（ロケールに依存しない解析）
    /// </summary>
    public decimal Quantity => CsvParsingHelper.ParseDecimal(QuantityString);
    
    /// <summary>
    /// 単価（ロケールに依存しない解析）
    /// </summary>
    public decimal UnitPrice => CsvParsingHelper.ParseDecimal(UnitPriceString);
    
    /// <summary>
    /// 金額（ロケールに依存しない解析）
    /// </summary>
    public decimal Amount => CsvParsingHelper.ParseDecimal(AmountString);
    
    [Name("手入力項目(半角8文字)")]
    [Index(152)]  // 153列目
    public string HandInputItem { get; set; } = string.Empty;

    /// <summary>
    /// 前月末在庫として有効かどうかを判定
    /// </summary>
    public bool IsValid()
    {
        // 商品コード00000は除外
        if (ProductCodeValidator.IsExcludedProductCode(ProductCode))
        {
            return false;
        }

        // 必須項目チェック
        // 商品コードは空文字列のみ無効
        if (string.IsNullOrEmpty(ProductCode))
        {
            return false;
        }
        
        // 等級・階級コードはnullのみ無効（空白文字は有効）
        // 荷印コードは任意項目のため検証しない
        if (GradeCode == null || ClassCode == null)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// バリデーション失敗の理由を取得
    /// </summary>
    public string GetValidationError()
    {
        if (ProductCodeValidator.IsExcludedProductCode(ProductCode))
        {
            return $"商品コード00000で除外: {ProductCode}";
        }

        if (string.IsNullOrEmpty(ProductCode))
        {
            return "商品コードが空";
        }
        if (GradeCode == null)
        {
            return "等級コードがnull";
        }
        if (ClassCode == null)
        {
            return "階級コードがnull";
        }
        // 荷印コードは任意項目のため検証しない

        return "有効";
    }

    /// <summary>
    /// 正規化された在庫キーを生成
    /// </summary>
    public (string ProductCode, string GradeCode, string ClassCode, string ShippingMarkCode, string ManualShippingMark) GetNormalizedKey()
    {
        return (
            ProductCode: (ProductCode ?? "").Trim().PadLeft(5, '0'),
            GradeCode: (GradeCode ?? "").Trim().PadLeft(3, '0'),
            ClassCode: (ClassCode ?? "").Trim().PadLeft(3, '0'),
            ShippingMarkCode: ShippingMarkCode ?? "    ",  // 空白4文字をデフォルトとし、Trimしない
            // 荷印名は手入力項目（153列目、Index=152）から取得する
            // ※CSV内の142列目の「荷印名」フィールドは使用しない（マスタ参照値のため）
            // 伝票に直接入力された値を8桁固定で使用
            ManualShippingMark: NormalizeManualShippingMark(HandInputItem)
        );
    }

    /// <summary>
    /// 日付文字列をDateTimeに変換（YYYYMMDD形式対応）
    /// </summary>
    public static DateTime ParseDate(string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
            return DateTime.Today;
        
        // YYYYMMDD形式の日付を解析
        if (dateStr.Length == 8 && int.TryParse(dateStr, out _))
        {
            if (DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }
        
        // その他の形式も試す
        if (DateTime.TryParse(dateStr, out var parsedDate))
        {
            return parsedDate.Date;
        }
        
        return DateTime.Today;
    }

    /// <summary>
    /// 荷印名（手入力項目）を正規化する
    /// 1. 全角スペースを半角スペースに変換
    /// 2. 後方の空白をトリム
    /// 3. 8桁固定長に調整
    /// </summary>
    private static string NormalizeManualShippingMark(string? input)
    {
        if (input == null) return "        "; // 8桁空白
        
        // 1. 全角スペースを半角スペースに変換
        var normalized = input.Replace('　', ' ');
        
        // 2. 後方の空白をトリム
        normalized = normalized.TrimEnd();
        
        // 3. 空文字の場合は8桁空白
        if (string.IsNullOrEmpty(normalized))
            return "        ";
        
        // 4. 8桁に調整（超過分は切り詰め、不足分は空白で埋める）
        if (normalized.Length >= 8)
            return normalized.Substring(0, 8);
        else
            return normalized.PadRight(8, ' ');
    }
}