using System.Globalization;
using CsvHelper.Configuration.Attributes;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Constants;
using InventorySystem.Import.Validators;
using InventorySystem.Import.Helpers;

namespace InventorySystem.Import.Models;

/// <summary>
/// 販売大臣AX受注伝票（在庫調整）CSVマッピングクラス（171列フォーマット）
/// 日本語ヘッダーとインデックス指定の両方に対応
/// </summary>
public class InventoryAdjustmentDaijinCsv
{
    [Name("伝票日付")]
    [Index(0)]
    public string VoucherDate { get; set; } = string.Empty;
    
    [Name("伝票区分(71:在庫調整)")]
    [Index(1)]
    public string VoucherType { get; set; } = string.Empty;
    
    [Name("伝票番号")]
    [Index(2)]
    public string VoucherNumber { get; set; } = string.Empty;
    
    [Name("システムデート")]
    [Index(46)]  // 47列目
    public string SystemDate { get; set; } = string.Empty;
    
    [Name("ジョブデート")]
    [Index(47)]  // 48列目
    public string JobDate { get; set; } = string.Empty;
    
    [Name("明細種(1固定)")]
    [Index(80)]  // 81列目
    public string DetailType { get; set; } = string.Empty;
    
    [Name("得意先コード")]
    [Index(6)]
    public string CustomerCode { get; set; } = string.Empty;
    
    [Name("得意先名１")]
    [Index(7)]
    public string CustomerName { get; set; } = string.Empty;
    
    [Name("商品コード")]
    [Index(89)]  // 90列目
    public string ProductCode { get; set; } = string.Empty;
    
    [Name("商品名")]
    [Index(145)]  // 146列目
    public string ProductName { get; set; } = string.Empty;
    
    [Name("等級コード")]
    [Index(83)]  // 84列目
    public string GradeCode { get; set; } = string.Empty;
    
    [Name("等級名")]
    [Index(141)]  // 142列目
    public string GradeName { get; set; } = string.Empty;
    
    [Name("階級コード")]
    [Index(84)]  // 85列目
    public string ClassCode { get; set; } = string.Empty;
    
    [Name("階級名")]
    [Index(142)]  // 143列目
    public string ClassName { get; set; } = string.Empty;
    
    [Name("荷印コード")]
    [Index(85)]  // 86列目
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    [Name("荷印名")]
    [Index(143)]  // 144列目
    public string ShippingMarkName { get; set; } = string.Empty;
    
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
    
    [Name("１階層目行番号")]
    [Index(75)]  // 76列目（受注伝票の場合）
    public int? Level1LineNumber { get; set; }

    [Name("２階層目行番号")]
    [Index(76)]  // 77列目
    public int? Level2LineNumber { get; set; }

    [Name("３階層目行番号")]
    [Index(77)]  // 78列目
    public int? Level3LineNumber { get; set; }

    [Name("４階層目行番号")]
    [Index(78)]  // 79列目
    public int? Level4LineNumber { get; set; }

    [Name("５階層目行番号")]
    [Index(79)]  // 80列目
    public int? Level5LineNumber { get; set; }
    
    // 商品分類は販売大臣のCSVに含まれない可能性があるため、デフォルト値を設定
    public string ProductCategory1 { get; set; } = "";
    public string ProductCategory2 { get; set; } = "";
    public string ProductCategory3 { get; set; } = "";

    /// <summary>
    /// CSVデータをInventoryAdjustmentエンティティに変換
    /// </summary>
    public InventoryAdjustment ToEntity(string dataSetId)
    {
        var inventoryAdjustment = new InventoryAdjustment
        {
            DataSetId = dataSetId,
            VoucherNumber = VoucherNumber?.Trim() ?? string.Empty,
            VoucherDate = ParseDate(VoucherDate),
            JobDate = ParseDate(JobDate),
            VoucherType = ConvertVoucherType(VoucherType?.Trim() ?? string.Empty),
            DetailType = ConvertDetailType(DetailType?.Trim() ?? string.Empty),
            CustomerCode = CustomerCode?.Trim(),
            CustomerName = CustomerName?.Trim(),
            ProductCode = ProductCode?.Trim() ?? string.Empty,
            ProductName = ProductName?.Trim(),
            GradeCode = GradeCode?.Trim() ?? string.Empty,
            ClassCode = ClassCode?.Trim() ?? string.Empty,
            ShippingMarkCode = ShippingMarkCode?.Trim() ?? string.Empty,
            // 荷印名は手入力項目（157列目、Index=156）から取得する
            // ※CSV内の141列目の「荷印名」フィールドは使用しない（マスタ参照値のため）
            // 伝票に直接入力された値を8桁固定で使用
            ShippingMarkName = (HandInputItem ?? "").PadRight(8).Substring(0, 8),
            CategoryCode = ParseCategoryCode(CategoryCode),
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            Amount = Amount,
            ProductCategory1 = ProductCategory1?.Trim(),
            ProductCategory2 = ProductCategory2?.Trim(),
            ProductCategory3 = ProductCategory3?.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        // LineNumberの設定（階層情報から決定）
        // 販売大臣の仕様：通常は5階層目に行番号が入る
        // 0より大きい値を持つ最も深い階層の値を使用
        int lineNumber = 1; // デフォルト値

        if (Level5LineNumber.HasValue && Level5LineNumber.Value > 0)
            lineNumber = Level5LineNumber.Value;
        else if (Level4LineNumber.HasValue && Level4LineNumber.Value > 0)
            lineNumber = Level4LineNumber.Value;
        else if (Level3LineNumber.HasValue && Level3LineNumber.Value > 0)
            lineNumber = Level3LineNumber.Value;
        else if (Level2LineNumber.HasValue && Level2LineNumber.Value > 0)
            lineNumber = Level2LineNumber.Value;
        else if (Level1LineNumber.HasValue && Level1LineNumber.Value > 0)
            lineNumber = Level1LineNumber.Value;

        inventoryAdjustment.LineNumber = lineNumber;
        
        // VoucherIdの設定（DataSetId、伝票番号、行番号を含む一意な値）
        inventoryAdjustment.VoucherId = $"{dataSetId}_{VoucherNumber}_{inventoryAdjustment.LineNumber}";

        // 特殊処理ルールを適用
        inventoryAdjustment.ApplySpecialProcessingRules();
        
        // 除外判定（フラグは立てるが、この段階では除外しない）
        if (inventoryAdjustment.ShouldBeExcluded())
        {
            inventoryAdjustment.IsExcluded = true;
            inventoryAdjustment.ExcludeReason = "荷印除外条件";
        }

        return inventoryAdjustment;
    }

    /// <summary>
    /// 消費税の集計行など、処理対象外の行かどうかを判定
    /// </summary>
    public bool IsSummaryRow()
    {
        // 商品コードが "00000" で、商品名に "消費税" が含まれる行は集計行とみなす
        return ProductCode == "00000" && (ProductName ?? "").Contains("消費税");
    }

    /// <summary>
    /// 在庫調整伝票として有効かどうかを判定
    /// </summary>
    public bool IsValidInventoryAdjustment()
    {
        // 伝票種別チェック（71:受注, 72:注文）
        if (VoucherType != "71" && VoucherType != "72")
        {
            return false;
        }

        // 明細種別チェック（1:受注のみ取込）
        if (DetailType != "1")
        {
            return false;
        }

        // 数量0は除外
        if (Quantity == 0)
        {
            return false;
        }

        // 商品コード00000は除外
        if (ProductCodeValidator.IsExcludedProductCode(ProductCode))
        {
            return false;
        }

        // 区分コードチェック（0-6すべて許可）
        // AdjustmentTypeの定義に従って有効な値をチェック
        var categoryCode = ParseCategoryCode(CategoryCode);
        if (categoryCode.HasValue)
        {
            var isValidType = Enum.IsDefined(typeof(AdjustmentType), categoryCode.Value);
            if (!isValidType)
            {
                return false;
            }
        }

        // 必須項目チェック（等級・階級・荷印コードが"000"の場合も許可）
        if (string.IsNullOrWhiteSpace(VoucherNumber) ||
            string.IsNullOrWhiteSpace(ProductCode))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 日付文字列をDateTimeに変換（ロケール非依存）
    /// </summary>
    private static DateTime ParseDate(string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
        {
            // 空の場合はエラーとして扱う（DateTime.TodayではなくMinValueを返す）
            return DateTime.MinValue;
        }
        
        // サポートする日付形式を定義（優先順）
        string[] dateFormats = new[]
        {
            "yyyy/MM/dd",     // CSVで最も使用される形式（例：2025/06/30）
            "yyyy-MM-dd",     // ISO形式
            "yyyyMMdd",       // 8桁数値形式
            "yyyy/M/d",       // 月日が1桁の場合
            "yyyy-M-d",       // ISO形式で月日が1桁
            "dd/MM/yyyy",     // ヨーロッパ形式（念のため）
            "dd.MM.yyyy"      // ドイツ語圏形式（念のため）
        };
        
        // InvariantCultureで複数形式を試行
        if (DateTime.TryParseExact(dateStr.Trim(), dateFormats, 
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }
        
        // 最終手段：InvariantCultureで標準解析
        if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture, 
            DateTimeStyles.None, out var parsedDate))
        {
            return parsedDate.Date;
        }
        
        // 解析失敗
        return DateTime.MinValue;
    }

    /// <summary>
    /// 伝票種別の変換
    /// </summary>
    private static string ConvertVoucherType(string voucherType)
    {
        return voucherType switch
        {
            "71" => "受注",
            "72" => "注文",
            _ => voucherType
        };
    }

    /// <summary>
    /// 明細種別の変換
    /// </summary>
    private static string ConvertDetailType(string detailType)
    {
        return detailType switch
        {
            "1" => "受注",
            _ => detailType
        };
    }

    /// <summary>
    /// 区分コードを数値に変換
    /// </summary>
    private static int? ParseCategoryCode(string categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
            return null;

        if (int.TryParse(categoryCode.Trim(), out var code))
            return code;

        return null;
    }
}