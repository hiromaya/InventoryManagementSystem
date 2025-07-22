using System.Globalization;
using CsvHelper.Configuration.Attributes;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Constants;
using InventorySystem.Import.Validators;
using InventorySystem.Import.Helpers;

namespace InventorySystem.Import.Models;

/// <summary>
/// 販売大臣AX売上伝票CSVマッピングクラス（171列フォーマット）
/// 日本語ヘッダーとインデックス指定の両方に対応
/// </summary>
public class SalesVoucherDaijinCsv
{
    [Name("伝票番号(自動採番)")]
    [Index(2)]  // 3列目
    public string VoucherNumber { get; set; } = string.Empty;
    
    [Name("伝票日付(西暦4桁YYYYMMDD)")]
    [Index(0)]  // 1列目
    public string VoucherDate { get; set; } = string.Empty;
    
    [Name("システムデート")]
    [Index(47)]  // 48列目（汎用日付1）
    public string SystemDate { get; set; } = string.Empty;
    
    [Name("ジョブデート")]
    [Index(48)]  // 49列目（汎用日付2）
    public string JobDate { get; set; } = string.Empty;
    
    [Name("伝票区分(51:掛売,52:現売)")]
    [Index(1)]  // 2列目
    public string VoucherType { get; set; } = string.Empty;
    
    [Name("明細種(1:売上,2:返品,4:値引)")]
    [Index(82)]  // 83列目（0ベースインデックス）
    public string DetailType { get; set; } = string.Empty;
    
    [Name("得意先コード")]
    [Index(7)]  // 8列目
    public string CustomerCode { get; set; } = string.Empty;
    
    [Name("得意先名１")]
    [Index(7)]  // CSV解析結果に基づく修正: 8から7に変更
    public string CustomerName { get; set; } = string.Empty;
    
    [Name("商品コード")]
    [Index(90)]  // 91列目
    public string ProductCode { get; set; } = string.Empty;
    
    [Name("商品名")]
    [Index(147)]  // 148列目
    public string ProductName { get; set; } = string.Empty;
    
    [Name("等級コード")]
    [Index(84)]  // 85列目
    public string GradeCode { get; set; } = string.Empty;
    
    [Name("等級名")]
    [Index(144)]  // 145列目
    public string GradeName { get; set; } = string.Empty;
    
    [Name("階級コード")]
    [Index(85)]  // 86列目
    public string ClassCode { get; set; } = string.Empty;
    
    [Name("階級名")]
    [Index(145)]  // 146列目
    public string ClassName { get; set; } = string.Empty;
    
    [Name("荷印コード")]
    [Index(86)]  // 87列目
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    [Name("荷印名")]
    [Index(146)]  // 147列目
    public string ShippingMarkName { get; set; } = string.Empty;
    
    [Name("数量")]
    [Index(95)]  // 96列目
    public string QuantityString { get; set; } = string.Empty;
    
    [Name("単価")]
    [Index(97)]  // 98列目
    public string UnitPriceString { get; set; } = string.Empty;
    
    [Name("金額")]
    [Index(98)]  // 99列目
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
    
    /// <summary>
    /// 粗利益（ロケールに依存しない解析）
    /// </summary>
    public decimal GrossProfit => CsvParsingHelper.ParseDecimal(GrossProfitString);
    
    /// <summary>
    /// 歩引き金（ロケールに依存しない解析）
    /// </summary>
    public decimal WalkingDiscount => CsvParsingHelper.ParseDecimal(WalkingDiscountString);
    
    [Name("手入力項目(半角8文字)")]
    [Index(154)]  // 155列目
    public string HandInputItem { get; set; } = string.Empty;
    
    [Name("汎用数値１（明細）")]
    [Index(125)]  // 126列目（粗利益）
    public string GrossProfitString { get; set; } = string.Empty;
    
    [Name("汎用数値２（明細）")]
    [Index(126)]  // 127列目（歩引き金）
    public string WalkingDiscountString { get; set; } = string.Empty;
    
    [Name("汎用マスター５コード（明細）")]
    [Index(88)]  // 89列目（商品分類5）
    public string ProductCategory5 { get; set; } = string.Empty;
    
    [Name("１階層目行番号")]
    [Index(78)]  // 79列目
    public int? Level1LineNumber { get; set; }

    [Name("２階層目行番号")]
    [Index(79)]  // 80列目
    public int? Level2LineNumber { get; set; }

    [Name("３階層目行番号")]
    [Index(80)]  // 81列目
    public int? Level3LineNumber { get; set; }

    [Name("４階層目行番号")]
    [Index(81)]  // 82列目
    public int? Level4LineNumber { get; set; }

    [Name("５階層目行番号")]
    [Index(82)]  // 83列目
    public int? Level5LineNumber { get; set; }
    
    // 商品分類は販売大臣のCSVに含まれない可能性があるため、デフォルト値を設定
    public string ProductCategory1 { get; set; } = "";
    public string ProductCategory2 { get; set; } = "";
    public string ProductCategory3 { get; set; } = "";

    /// <summary>
    /// CSVデータをSalesVoucherエンティティに変換
    /// </summary>
    public SalesVoucher ToEntity(string dataSetId)
    {
        var salesVoucher = new SalesVoucher
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
            ShippingMarkCode = ShippingMarkCode ?? "    ",  // 空白4文字をデフォルトとし、Trimしない
            // 荷印名は手入力項目（153列目、Index=152）から取得する
            // ※CSV内の141列目の「荷印名」フィールドは使用しない（マスタ参照値のため）
            // 伝票に直接入力された値を8桁固定で使用
            ShippingMarkName = HandInputItem ?? "        ",  // 空白8文字をデフォルトとし、Trimしない
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            Amount = Amount,
            ProductCategory1 = ProductCategory1?.Trim(),
            ProductCategory2 = ProductCategory2?.Trim(),
            ProductCategory3 = ProductCategory3?.Trim(),
            ProductCategory5 = ProductCategory5?.Trim(),
            GrossProfit = GrossProfit,
            WalkingDiscount = WalkingDiscount,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        // InventoryKey設定
        salesVoucher.InventoryKey = new InventoryKey
        {
            ProductCode = salesVoucher.ProductCode,
            GradeCode = salesVoucher.GradeCode,
            ClassCode = salesVoucher.ClassCode,
            ShippingMarkCode = salesVoucher.ShippingMarkCode,
            ShippingMarkName = salesVoucher.ShippingMarkName
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

        salesVoucher.LineNumber = lineNumber;
        
        // VoucherIdの設定（DataSetId、伝票番号、行番号を含む一意な値）
        salesVoucher.VoucherId = $"{dataSetId}_{VoucherNumber}_{salesVoucher.LineNumber}";

        // 特殊処理ルールを適用
        salesVoucher.ApplySpecialProcessingRules();
        
        // 除外判定（フラグは立てるが、この段階では除外しない）
        if (salesVoucher.ShouldBeExcluded())
        {
            salesVoucher.IsExcluded = true;
            salesVoucher.ExcludeReason = "荷印除外条件";
        }

        return salesVoucher;
    }

    /// <summary>
    /// 売上伝票として有効かどうかを判定
    /// </summary>
    public bool IsValidSalesVoucher()
    {
        // 伝票種別チェック（51:掛売上, 52:現金売上）
        if (VoucherType != "51" && VoucherType != "52")
        {
            return false;
        }

        // 明細種別チェック（1:商品, 2:返品, 3:単品値引, 4:値引を取込）
        // 注意：要件定義書では明細種別4（値引）は「処理しない」とあるが、
        // これは後の処理段階（商品勘定など）での話であり、
        // CSVインポート時には取り込む必要がある
        if (DetailType != "1" && DetailType != "2" && DetailType != "3" && DetailType != "4")
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

        // 必須項目チェック（販売大臣仕様準拠）
        
        // 伝票番号は空文字列のみ無効
        if (string.IsNullOrEmpty(VoucherNumber))
        {
            return false;
        }
        
        // 商品コードは空文字列またはオール0が無効
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
        // 伝票種別チェック（51:掛売, 52:現売のみ取込）
        if (VoucherType != "51" && VoucherType != "52")
        {
            return $"無効な伝票種別: {VoucherType} (許可: 51, 52)";
        }

        // 明細種別チェック（1:商品, 2:返品, 3:単品値引, 4:値引を取込）
        if (DetailType != "1" && DetailType != "2" && DetailType != "3" && DetailType != "4")
        {
            return $"無効な明細種別: {DetailType} (許可: 1, 2, 3, 4)";
        }

        // 数量0は除外
        if (Quantity == 0)
        {
            return "数量が0";
        }

        // 商品コード00000は除外
        if (ProductCodeValidator.IsExcludedProductCode(ProductCode))
        {
            return $"除外商品コード: {ProductCode}";
        }

        // 必須項目チェック（販売大臣仕様準拠）
        
        // 伝票番号は空文字列のみ無効
        if (string.IsNullOrEmpty(VoucherNumber))
        {
            return "伝票番号が空";
        }
        
        // 商品コードは空文字列のみ無効（オール0は別途チェック）
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
               $"商品コード={ProductCode}, 得意先コード={CustomerCode}, " +
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
            "51" => SalesVoucherTypes.Credit,  // 掛売上
            "52" => SalesVoucherTypes.Cash,    // 現金売上
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
            "1" => DetailTypes.Product,   // 売上
            "2" => DetailTypes.Return,    // 返品
            "4" => DetailTypes.Discount,  // 値引
            _ => detailType
        };
    }
}