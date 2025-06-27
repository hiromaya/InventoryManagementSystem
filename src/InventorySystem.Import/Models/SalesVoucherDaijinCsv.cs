using System.Globalization;
using CsvHelper.Configuration.Attributes;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Constants;
using InventorySystem.Import.Validators;

namespace InventorySystem.Import.Models;

/// <summary>
/// 販売大臣AX売上伝票CSVマッピングクラス（171列フォーマット）
/// 日本語ヘッダーとインデックス指定の両方に対応
/// </summary>
public class SalesVoucherDaijinCsv
{
    [Name("伝票番号(自動採番)")]
    [Index(0)]
    public string VoucherNumber { get; set; } = string.Empty;
    
    [Name("伝票日付(西暦4桁YYYYMMDD)")]
    [Index(1)]
    public string VoucherDate { get; set; } = string.Empty;
    
    [Name("ジョブデート")]
    [Index(48)]  // 49列目
    public string JobDate { get; set; } = string.Empty;
    
    [Name("伝票区分(51:掛売,52:現売)")]
    [Index(2)]
    public string VoucherType { get; set; } = string.Empty;
    
    [Name("明細種(1:売上,2:返品,4:値引)")]
    [Index(83)]  // 84列目
    public string DetailType { get; set; } = string.Empty;
    
    [Name("得意先コード")]
    [Index(3)]
    public string CustomerCode { get; set; } = string.Empty;
    
    [Name("得意先名１")]
    [Index(7)]  // CSV解析結果に基づく修正: 8から7に変更
    public string CustomerName { get; set; } = string.Empty;
    
    [Name("商品コード")]
    [Index(93)]  // 94列目
    public string ProductCode { get; set; } = string.Empty;
    
    [Name("商品名")]
    [Index(148)]  // 149列目
    public string ProductName { get; set; } = string.Empty;
    
    [Name("等級コード")]
    [Index(87)]  // 88列目
    public string GradeCode { get; set; } = string.Empty;
    
    [Name("等級名")]
    [Index(145)]  // 146列目
    public string GradeName { get; set; } = string.Empty;
    
    [Name("階級コード")]
    [Index(88)]  // 89列目
    public string ClassCode { get; set; } = string.Empty;
    
    [Name("階級名")]
    [Index(146)]  // 147列目
    public string ClassName { get; set; } = string.Empty;
    
    [Name("荷印コード")]
    [Index(89)]  // 90列目
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    [Name("荷印名")]
    [Index(146)]  // 147列目
    public string ShippingMarkName { get; set; } = string.Empty;
    
    [Name("数量")]
    [Index(98)]  // 99列目
    public decimal Quantity { get; set; }
    
    [Name("単価")]
    [Index(100)]  // 101列目
    public decimal UnitPrice { get; set; }
    
    [Name("金額")]
    [Index(101)]  // 102列目
    public decimal Amount { get; set; }
    
    [Name("手入力項目(半角8文字)")]
    [Index(157)]  // 158列目
    public string HandInputItem { get; set; } = string.Empty;
    
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
        salesVoucher.InventoryKey = new InventoryKey
        {
            ProductCode = salesVoucher.ProductCode,
            GradeCode = salesVoucher.GradeCode,
            ClassCode = salesVoucher.ClassCode,
            ShippingMarkCode = salesVoucher.ShippingMarkCode,
            ShippingMarkName = salesVoucher.ShippingMarkName
        };

        // VoucherIdとLineNumberの生成（簡易実装）
        salesVoucher.VoucherId = $"{VoucherNumber}_{VoucherDate}";
        salesVoucher.LineNumber = 1; // 実際のCSVに行番号がある場合は適切に設定

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

        // 明細種別チェック（1:商品, 2:返品, 4:値引のみ取込）
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