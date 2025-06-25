using System.Globalization;
using CsvHelper.Configuration.Attributes;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Constants;

namespace InventorySystem.Import.Models;

/// <summary>
/// 販売大臣AX受注伝票（在庫調整）CSVマッピングクラス（171列フォーマット）
/// 日本語ヘッダーとインデックス指定の両方に対応
/// </summary>
public class InventoryAdjustmentDaijinCsv
{
    [Name("伝票番号(自動採番)")]
    [Index(0)]
    public string VoucherNumber { get; set; } = string.Empty;
    
    [Name("伝票日付(西暦4桁YYYYMMDD)")]
    [Index(1)]
    public string VoucherDate { get; set; } = string.Empty;
    
    [Name("ジョブデート")]
    [Index(47)]  // 48列目
    public string JobDate { get; set; } = string.Empty;
    
    [Name("伝票区分(71:受注,72:注文)")]
    [Index(2)]
    public string VoucherType { get; set; } = string.Empty;
    
    [Name("明細種(1:受注)")]
    [Index(80)]  // 81列目
    public string DetailType { get; set; } = string.Empty;
    
    [Name("得意先コード")]
    [Index(3)]
    public string CustomerCode { get; set; } = string.Empty;
    
    [Name("得意先名１")]
    [Index(8)]
    public string CustomerName { get; set; } = string.Empty;
    
    [Name("商品コード")]
    [Index(88)]  // 89列目
    public string ProductCode { get; set; } = string.Empty;
    
    [Name("商品名")]
    [Index(142)]  // 143列目
    public string ProductName { get; set; } = string.Empty;
    
    [Name("等級コード")]
    [Index(82)]  // 83列目
    public string GradeCode { get; set; } = string.Empty;
    
    [Name("等級名")]
    [Index(136)]  // 137列目
    public string GradeName { get; set; } = string.Empty;
    
    [Name("階級コード")]
    [Index(83)]  // 84列目
    public string ClassCode { get; set; } = string.Empty;
    
    [Name("階級名")]
    [Index(137)]  // 138列目
    public string ClassName { get; set; } = string.Empty;
    
    [Name("荷印コード")]
    [Index(84)]  // 85列目
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    [Name("荷印名")]
    [Index(138)]  // 139列目
    public string ShippingMarkName { get; set; } = string.Empty;
    
    [Name("数量")]
    [Index(93)]  // 94列目
    public decimal Quantity { get; set; }
    
    [Name("単価")]
    [Index(95)]  // 96列目
    public decimal UnitPrice { get; set; }
    
    [Name("金額")]
    [Index(96)]  // 97列目
    public decimal Amount { get; set; }
    
    // 区分コード（1:ロス, 4:振替, 6:調整, 2:経費, 5:加工）
    [Name("区分コード")]
    [Index(100)]  // 仮のインデックス、実際のCSVに合わせて調整
    public int? CategoryCode { get; set; }
    
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
            ShippingMarkName = ShippingMarkName?.Trim() ?? string.Empty,
            CategoryCode = CategoryCode,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            Amount = Amount,
            ProductCategory1 = ProductCategory1?.Trim(),
            ProductCategory2 = ProductCategory2?.Trim(),
            ProductCategory3 = ProductCategory3?.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        // InventoryAdjustmentエンティティには特別な設定は不要

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

        // 区分コードチェック（2:経費, 5:加工は除外）
        if (CategoryCode.HasValue && (CategoryCode.Value == 2 || CategoryCode.Value == 5))
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
}