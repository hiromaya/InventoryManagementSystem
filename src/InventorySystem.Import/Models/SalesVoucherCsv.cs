using CsvHelper.Configuration.Attributes;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Constants;

namespace InventorySystem.Import.Models;

/// <summary>
/// 売上伝票CSVマッピングクラス
/// 販売大臣AXの売上伝票CSV構造に対応
/// </summary>
public class SalesVoucherCsv
{
    [Index(0)]
    public string VoucherNumber { get; set; } = string.Empty;
    
    [Index(1)]
    public string VoucherDate { get; set; } = string.Empty;
    
    [Index(2)]
    public string JobDate { get; set; } = string.Empty;  // 汎用日付2
    
    [Index(3)]
    public string VoucherType { get; set; } = string.Empty;
    
    [Index(4)]
    public string DetailType { get; set; } = string.Empty;
    
    [Index(5)]
    public string CustomerCode { get; set; } = string.Empty;
    
    [Index(6)]
    public string CustomerName { get; set; } = string.Empty;
    
    [Index(7)]
    public string ProductCode { get; set; } = string.Empty;
    
    [Index(8)]
    public string ProductName { get; set; } = string.Empty;
    
    [Index(9)]
    public string GradeCode { get; set; } = string.Empty;
    
    [Index(10)]
    public string ClassCode { get; set; } = string.Empty;
    
    [Index(11)]
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    [Index(12)]
    public string ShippingMarkName { get; set; } = string.Empty;
    
    [Index(13)]
    public decimal Quantity { get; set; }
    
    [Index(14)]
    public decimal UnitPrice { get; set; }
    
    [Index(15)]
    public decimal Amount { get; set; }
    
    [Index(16)]
    public string ProductCategory1 { get; set; } = string.Empty;
    
    [Index(17)]
    public string ProductCategory2 { get; set; } = string.Empty;
    
    [Index(18)]
    public string ProductCategory3 { get; set; } = string.Empty;
    
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
            VoucherType = VoucherType?.Trim() ?? string.Empty,
            DetailType = DetailType?.Trim() ?? string.Empty,
            CustomerCode = CustomerCode?.Trim(),
            CustomerName = CustomerName?.Trim(),
            ProductCode = ProductCode?.Trim() ?? string.Empty,
            ProductName = ProductName?.Trim(),
            GradeCode = GradeCode?.Trim() ?? string.Empty,
            ClassCode = ClassCode?.Trim() ?? string.Empty,
            ShippingMarkCode = ShippingMarkCode?.Trim() ?? string.Empty,
            ShippingMarkName = ShippingMarkName?.Trim() ?? string.Empty,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            Amount = Amount,
            ProductCategory1 = ProductCategory1?.Trim(),
            ProductCategory2 = ProductCategory2?.Trim(),
            ProductCategory3 = ProductCategory3?.Trim(),
            ImportedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

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
        if (VoucherType != SalesVoucherTypes.Credit && VoucherType != SalesVoucherTypes.Cash)
        {
            return false;
        }

        // 明細種別チェック（1:商品, 2:返品, 3:値引のみ取込）
        if (DetailType != DetailTypes.Product && 
            DetailType != DetailTypes.Return && 
            DetailType != DetailTypes.Discount)
        {
            return false;
        }

        // 数量0は除外
        if (Quantity == 0)
        {
            return false;
        }

        // 必須項目チェック
        if (string.IsNullOrWhiteSpace(VoucherNumber) ||
            string.IsNullOrWhiteSpace(ProductCode) ||
            string.IsNullOrWhiteSpace(GradeCode) ||
            string.IsNullOrWhiteSpace(ClassCode) ||
            string.IsNullOrWhiteSpace(ShippingMarkCode) ||
            string.IsNullOrWhiteSpace(ShippingMarkName))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 日付文字列をDateTimeに変換
    /// </summary>
    private static DateTime ParseDate(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return DateTime.Today;
        }

        // yyyy/MM/dd, yyyy-MM-dd, yyyyMMdd 形式に対応
        if (DateTime.TryParse(dateString, out var result))
        {
            return result.Date;
        }

        throw new FormatException($"日付の解析に失敗しました: {dateString}");
    }
}