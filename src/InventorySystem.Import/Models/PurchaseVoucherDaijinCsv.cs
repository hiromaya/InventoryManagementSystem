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
    [Index(0)]
    public string VoucherNumber { get; set; } = string.Empty;
    
    [Name("伝票日付")]
    [Index(1)]
    public string VoucherDate { get; set; } = string.Empty;
    
    [Name("ジョブデート")]
    [Index(44)]  // 45列目
    public string JobDate { get; set; } = string.Empty;
    
    [Name("伝票区分(11:掛仕入,12:現金仕入)")]
    [Index(2)]
    public string VoucherType { get; set; } = string.Empty;
    
    [Name("明細種")]
    [Index(79)]  // 80列目
    public string DetailType { get; set; } = string.Empty;
    
    [Name("仕入先コード")]
    [Index(3)]
    public string SupplierCode { get; set; } = string.Empty;
    
    [Name("仕入先名")]
    [Index(7)]  // CSV解析結果に基づく修正: 8から7に変更
    public string SupplierName { get; set; } = string.Empty;
    
    [Name("商品コード")]
    [Index(87)]  // 88列目
    public string ProductCode { get; set; } = string.Empty;
    
    [Name("商品名")]
    [Index(140)]  // 141列目
    public string ProductName { get; set; } = string.Empty;
    
    [Name("等級コード")]
    [Index(81)]  // 82列目
    public string GradeCode { get; set; } = string.Empty;
    
    [Name("等級名")]
    [Index(134)]  // 135列目
    public string GradeName { get; set; } = string.Empty;
    
    [Name("階級コード")]
    [Index(82)]  // 83列目
    public string ClassCode { get; set; } = string.Empty;
    
    [Name("階級名")]
    [Index(135)]  // 136列目
    public string ClassName { get; set; } = string.Empty;
    
    [Name("荷印コード")]
    [Index(83)]  // 84列目
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    [Name("荷印名")]
    [Index(136)]  // 137列目
    public string ShippingMarkName { get; set; } = string.Empty;
    
    [Name("数量")]
    [Index(92)]  // 93列目
    public decimal Quantity { get; set; }
    
    [Name("単価")]
    [Index(94)]  // 95列目
    public decimal UnitPrice { get; set; }
    
    [Name("金額")]
    [Index(95)]  // 96列目
    public decimal Amount { get; set; }
    
    [Name("荷印手入力")]
    [Index(147)]  // 148列目
    public string HandInputItem { get; set; } = string.Empty;
    
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
            ShippingMarkName = (HandInputItem ?? "").PadRight(8).Substring(0, 8),  // 手入力項目を荷印手入力として使用（8桁固定）
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

        // VoucherIdとLineNumberの生成（簡易実装）
        purchaseVoucher.VoucherId = $"{VoucherNumber}_{VoucherDate}";
        purchaseVoucher.LineNumber = 1; // 実際のCSVに行番号がある場合は適切に設定

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