using CsvHelper.Configuration.Attributes;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Constants;

namespace InventorySystem.Import.Models;

/// <summary>
/// 仕入伝票CSVマッピングクラス
/// 販売大臣AXの仕入伝票CSV構造に対応
/// </summary>
public class PurchaseVoucherCsv
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
    public string SupplierCode { get; set; } = string.Empty;
    
    [Index(6)]
    public string SupplierName { get; set; } = string.Empty;
    
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
            VoucherType = VoucherType?.Trim() ?? string.Empty,
            DetailType = DetailType?.Trim() ?? string.Empty,
            SupplierCode = SupplierCode?.Trim(),
            SupplierName = SupplierName?.Trim(),
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
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

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
        if (VoucherType != PurchaseVoucherTypes.Credit && VoucherType != PurchaseVoucherTypes.Cash)
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

        // 必須項目チェック（等級・階級コードが"000"の場合は空として扱い、許可する）
        if (string.IsNullOrWhiteSpace(VoucherNumber) ||
            string.IsNullOrWhiteSpace(ProductCode))
        {
            return false;
        }

        // 等級・階級コードは"000"または空を許可しない（ただし"000"は許可）
        // ※販売大臣では等級・階級が"000"の場合があるため、これを許可する

        // 荷印コード・荷印名は必須
        if (string.IsNullOrWhiteSpace(ShippingMarkCode) ||
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