using System.Globalization;
using CsvHelper.Configuration.Attributes;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Constants;
using InventorySystem.Import.Validators;

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
    [Index(79)]  // 80列目
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
    [Index(140)]  // 141列目
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
    public decimal Quantity { get; set; }
    
    [Name("単価")]
    [Index(93)]  // 94列目
    public decimal UnitPrice { get; set; }
    
    [Name("金額")]
    [Index(94)]  // 95列目
    public decimal Amount { get; set; }
    
    [Name("荷印手入力")]
    [Index(146)]  // 147列目（0ベースインデックス）
    public string HandInputItem { get; set; } = string.Empty;  // 荷印手入力
    
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
            SupplierCode = SupplierCode?.Trim(),
            SupplierName = SupplierName?.Trim(),
            ProductCode = ProductCode?.Trim() ?? string.Empty,
            ProductName = ProductName?.Trim(),
            GradeCode = GradeCode?.Trim() ?? string.Empty,
            ClassCode = ClassCode?.Trim() ?? string.Empty,
            ShippingMarkCode = ShippingMarkCode?.Trim() ?? string.Empty,
            // 荷印名は手入力項目（147列目、Index=146）から取得する
            // ※CSV内の141列目の「荷印名」フィールドは使用しない（マスタ参照値のため）
            // 伝票に直接入力された値を8桁固定で使用
            ShippingMarkName = (HandInputItem ?? "").PadRight(8).Substring(0, 8),
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
            ShippingMarkName = purchaseVoucher.ShippingMarkName
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

        // 明細種別チェック（1:仕入, 2:返品, 4:値引のみ取込）
        if (DetailType != "1" && DetailType != "2" && DetailType != "4")
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

        // 仕入先コード00000は除外
        if (CodeValidator.IsExcludedCode(SupplierCode))
        {
            return false;
        }

        // 必須項目チェック
        if (string.IsNullOrWhiteSpace(VoucherNumber) ||
            string.IsNullOrWhiteSpace(ProductCode) ||
            string.IsNullOrWhiteSpace(GradeCode) ||
            string.IsNullOrWhiteSpace(ClassCode) ||
            string.IsNullOrWhiteSpace(ShippingMarkCode))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 日付文字列をDateTimeに変換（YYYYMMDD形式対応）
    /// </summary>
    private static DateTime ParseDate(string dateStr)
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
}