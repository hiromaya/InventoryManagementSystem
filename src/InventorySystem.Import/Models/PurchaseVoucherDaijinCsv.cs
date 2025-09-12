using System.Globalization;
using CsvHelper.Configuration.Attributes;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Constants;
using InventorySystem.Import.Validators;
using InventorySystem.Import.Helpers;

namespace InventorySystem.Import.Models;

/// <summary>
/// 販売大臣AX仕入伝票CSVマッピングクラス（171列フォーマット）
/// 日本語ヘッダーとインデックス指定の両方に対応
/// </summary>
public class PurchaseVoucherDaijinCsv
{
    [Name("伝票番号")]
    [Index(2)]  // 3列目
    public string VoucherNumber { get; set; } = string.Empty;
    
    [Name("伝票日付")]
    [Index(0)]  // 1列目
    public string VoucherDate { get; set; } = string.Empty;
    
    [Name("システムデート")]
    [Index(42)]  // 43列目（汎用日付1）
    public string SystemDate { get; set; } = string.Empty;
    
    [Name("ジョブデート")]
    [Index(43)]  // 44列目
    public string JobDate { get; set; } = string.Empty;
    
    [Name("伝票区分(11:掛仕入,12:現金仕入)")]
    [Index(1)]  // 2列目
    public string VoucherType { get; set; } = string.Empty;
    
    [Name("明細種")]
    [Index(78)]  // 79列目（0ベースインデックス）
    public string DetailType { get; set; } = string.Empty;
    
    [Name("仕入先コード")]
    [Index(6)]  // 7列目
    public string SupplierCode { get; set; } = string.Empty;
    
    [Name("仕入先名")]
    [Index(7)]  // CSV解析結果に基づく修正: 8から7に変更
    public string SupplierName { get; set; } = string.Empty;
    
    [Name("商品コード")]
    [Index(86)]  // 87列目
    public string ProductCode { get; set; } = string.Empty;
    
    [Name("商品名")]
    [Index(139)]  // 140列目
    public string ProductName { get; set; } = string.Empty;
    
    [Name("等級コード")]
    [Index(80)]  // 81列目
    public string GradeCode { get; set; } = string.Empty;
    
    [Name("等級名")]
    [Index(134)]  // 135列目
    public string GradeName { get; set; } = string.Empty;
    
    [Name("階級コード")]
    [Index(81)]  // 82列目
    public string ClassCode { get; set; } = string.Empty;
    
    [Name("階級名")]
    [Index(135)]  // 136列目
    public string ClassName { get; set; } = string.Empty;
    
    [Name("荷印コード")]
    [Index(82)]  // 83列目
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    [Name("荷印名")]
    [Index(136)]  // 137列目
    public string ShippingMarkName { get; set; } = string.Empty;
    
    [Name("数量")]
    [Index(91)]  // 92列目
    public string QuantityString { get; set; } = string.Empty;
    
    [Name("単価")]
    [Index(93)]  // 94列目
    public string UnitPriceString { get; set; } = string.Empty;
    
    [Name("金額")]
    [Index(94)]  // 95列目
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
    
    [Name("荷印手入力")]
    [Index(146)]  // 147列目（0ベースインデックス）
    public string ManualShippingMark { get; set; } = string.Empty;  // 荷印手入力
    
    [Name("１階層目行番号")]
    [Index(74)]  // 75列目（仕入伝票の場合）
    public int? Level1LineNumber { get; set; }

    [Name("２階層目行番号")]
    [Index(75)]  // 76列目
    public int? Level2LineNumber { get; set; }

    [Name("３階層目行番号")]
    [Index(76)]  // 77列目
    public int? Level3LineNumber { get; set; }

    [Name("４階層目行番号")]
    [Index(77)]  // 78列目
    public int? Level4LineNumber { get; set; }

    [Name("５階層目行番号")]
    [Index(78)]  // 79列目
    public int? Level5LineNumber { get; set; }
    
    // 商品分類は販売大臣のCSVに含まれない可能性があるため、デフォルト値を設定
    public string ProductCategory1 { get; set; } = "";
    public string ProductCategory2 { get; set; } = "";
    public string ProductCategory3 { get; set; } = "";

    /// <summary>
    /// CSVデータをPurchaseVoucherエンティティに変換
    /// </summary>
    public PurchaseVoucher ToEntity(string dataSetId)
    {
        var purchaseVoucher = new PurchaseVoucher
        {
            DataSetId = dataSetId,
            VoucherNumber = VoucherNumber?.Trim() ?? string.Empty,
            VoucherDate = ParseDate(VoucherDate),
            JobDate = ParseDate(JobDate),
            VoucherType = ConvertVoucherType(VoucherType?.Trim() ?? string.Empty),
            DetailType = ConvertDetailType(DetailType?.Trim() ?? string.Empty),
            SupplierCode = CodeFormatter.FormatTo5Digits(SupplierCode),
            SupplierName = SupplierName?.Trim(),
            ProductCode = ProductCode?.Trim() ?? string.Empty,
            ProductName = ProductName?.Trim(),
            GradeCode = GradeCode?.Trim() ?? string.Empty,
            ClassCode = ClassCode?.Trim() ?? string.Empty,
            ShippingMarkCode = ShippingMarkCode ?? "    ",  // 空白4文字をデフォルトとし、Trimしない
            // 手入力項目を正規化して設定
            ManualShippingMark = NormalizeManualShippingMark(ManualShippingMark),
            // 荷印名はそのまま設定（マスタ参照値）
            ShippingMarkName = ShippingMarkName,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            Amount = Amount,
            ProductCategory1 = ProductCategory1?.Trim(),
            ProductCategory2 = ProductCategory2?.Trim(),
            ProductCategory3 = ProductCategory3?.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        // InventoryKey設定
        purchaseVoucher.InventoryKey = new InventoryKey
        {
            ProductCode = purchaseVoucher.ProductCode,
            GradeCode = purchaseVoucher.GradeCode,
            ClassCode = purchaseVoucher.ClassCode,
            ShippingMarkCode = purchaseVoucher.ShippingMarkCode,
            ManualShippingMark = purchaseVoucher.ManualShippingMark
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

        purchaseVoucher.LineNumber = lineNumber;
        
        // VoucherIdの設定（DataSetId、伝票番号、行番号を含む一意な値）
        purchaseVoucher.VoucherId = $"{dataSetId}_{VoucherNumber}_{purchaseVoucher.LineNumber}";

        // 特殊処理ルールを適用
        purchaseVoucher.ApplySpecialProcessingRules();
        
        // 除外判定（フラグは立てるが、この段階では除外しない）
        if (purchaseVoucher.ShouldBeExcluded())
        {
            purchaseVoucher.IsExcluded = true;
            purchaseVoucher.ExcludeReason = "荷印除外条件";
        }

        return purchaseVoucher;
    }

    /// <summary>
    /// 仕入伝票として有効かどうかを判定
    /// </summary>
    public bool IsValidPurchaseVoucher()
    {
        // 伝票種別チェック（11:掛仕入, 12:現金仕入）
        if (VoucherType != "11" && VoucherType != "12")
        {
            return false;
        }

        // 明細種別チェック（1:仕入, 2:返品, 3:単品値引のみ、4:値引は除外）
        // 要件定義書に基づき明細種別4（値引）は取り込まない
        if (DetailType != "1" && DetailType != "2" && DetailType != "3")
        {
            return false;
        }

        // 数量0チェック（明細種別3は例外）
        if (Quantity == 0 && DetailType != "3")
        {
            return false;
        }

        // 明細種別3（単品値引）で金額も0の場合は無効
        if (DetailType == "3" && Amount == 0)
        {
            return false;
        }

        // 商品コード00000は除外
        if (ProductCodeValidator.IsExcludedProductCode(ProductCode))
        {
            return false;
        }

        // 仕入先コード00000は除外
        if (CodeValidator.IsExcludedCode(SupplierCode))
        {
            return false;
        }

        // 必須項目チェック（販売大臣仕様準拠）
        
        // 伝票番号は空文字列のみ無効
        if (string.IsNullOrEmpty(VoucherNumber))
        {
            return false;
        }
        
        // 商品コードは空文字列が無効
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
    /// バリデーションエラーの詳細を取得
    /// </summary>
    public string GetValidationError()
    {
        // 伝票種別チェック（11:掛仕入, 12:現金仕入のみ取込）
        if (VoucherType != "11" && VoucherType != "12")
        {
            return $"無効な伝票種別: {VoucherType} (許可: 11, 12)";
        }

        // 明細種別チェック（1:仕入, 2:返品, 3:単品値引のみ、4:値引は除外）
        if (DetailType != "1" && DetailType != "2" && DetailType != "3")
        {
            return $"無効な明細種別: {DetailType} (許可: 1, 2, 3)";
        }

        // 数量チェック（明細種別3は除外）
        if (Quantity == 0 && DetailType != "3")
        {
            return "数量が0（単品値引以外）";
        }

        // 明細種別3の金額チェック
        if (DetailType == "3" && Amount == 0)
        {
            return "単品値引の金額が0";
        }

        // 商品コード00000は除外
        if (ProductCodeValidator.IsExcludedProductCode(ProductCode))
        {
            return $"除外商品コード: {ProductCode}";
        }

        // 仕入先コード00000は除外
        if (CodeValidator.IsExcludedCode(SupplierCode))
        {
            return $"除外仕入先コード: {SupplierCode}";
        }

        // 必須項目チェック（販売大臣仕様準拠）
        
        // 伝票番号は空文字列のみ無効
        if (string.IsNullOrEmpty(VoucherNumber))
        {
            return "伝票番号が空";
        }
        
        // 商品コードは空文字列のみ無効
        if (string.IsNullOrEmpty(ProductCode))
        {
            return "商品コードが空";
        }
        
        // 等級・階級コードはnullのみ無効（空白文字は有効）
        // 荷印コードは任意項目のため検証しない
        if (GradeCode == null)
        {
            return "等級コードがnull";
        }
        if (ClassCode == null)
        {
            return "階級コードがnull";
        }

        return "有効（エラーなし）";
    }

    /// <summary>
    /// デバッグ用の詳細情報を取得
    /// </summary>
    public string GetDebugInfo()
    {
        return $"伝票種別={VoucherType}, 明細種別={DetailType}, 数量={Quantity}, " +
               $"商品コード={ProductCode}, 仕入先コード={SupplierCode}, " +
               $"等級コード={GradeCode}, 階級コード={ClassCode}, 荷印コード={ShippingMarkCode}";
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
            "11" => PurchaseVoucherTypes.Credit,  // 掛仕入
            "12" => PurchaseVoucherTypes.Cash,    // 現金仕入
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
            "1" => DetailTypes.Product,   // 仕入
            "2" => DetailTypes.Return,    // 返品
            "4" => DetailTypes.Discount,  // 値引
            _ => detailType
        };
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