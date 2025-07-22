using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Reports.Models;

/// <summary>
/// 商品勘定帳票用データモデル
/// FastReportに渡すレポート専用DTO
/// </summary>
public class ProductAccountReportModel
{
    /// <summary>
    /// 商品コード
    /// </summary>
    [MaxLength(20)]
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// 商品名
    /// </summary>
    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 荷印コード
    /// </summary>
    [MaxLength(4)]
    public string ShippingMarkCode { get; set; } = string.Empty;

    /// <summary>
    /// 荷印名
    /// </summary>
    [MaxLength(50)]
    public string ShippingMarkName { get; set; } = string.Empty;

    /// <summary>
    /// 手入力荷印（8文字固定）
    /// </summary>
    [MaxLength(8)]
    public string ManualShippingMark { get; set; } = string.Empty;

    /// <summary>
    /// 等級コード
    /// </summary>
    [MaxLength(3)]
    public string GradeCode { get; set; } = string.Empty;

    /// <summary>
    /// 等級名
    /// </summary>
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;

    /// <summary>
    /// 階級コード
    /// </summary>
    [MaxLength(3)]
    public string ClassCode { get; set; } = string.Empty;

    /// <summary>
    /// 階級名
    /// </summary>
    [MaxLength(50)]
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// 伝票番号
    /// </summary>
    [MaxLength(15)]
    public string VoucherNumber { get; set; } = string.Empty;

    /// <summary>
    /// レコード種別（Previous/Purchase/Sales/Adjustment/Loss/Processing/Transfer）
    /// </summary>
    [MaxLength(20)]
    public string RecordType { get; set; } = string.Empty;

    /// <summary>
    /// 伝票種別（11,12,51,52,71等）
    /// </summary>
    [MaxLength(5)]
    public string VoucherCategory { get; set; } = string.Empty;

    /// <summary>
    /// 画面表示用区分（前残、掛仕、掛売等）
    /// </summary>
    [MaxLength(10)]
    public string DisplayCategory { get; set; } = string.Empty;

    /// <summary>
    /// 取引日付
    /// </summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// 仕入数量
    /// </summary>
    public decimal PurchaseQuantity { get; set; }

    /// <summary>
    /// 売上数量
    /// </summary>
    public decimal SalesQuantity { get; set; }

    /// <summary>
    /// 残数量
    /// </summary>
    public decimal RemainingQuantity { get; set; }

    /// <summary>
    /// 単価
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 金額
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 粗利益
    /// </summary>
    public decimal GrossProfit { get; set; }

    /// <summary>
    /// 取引先名（得意先または仕入先）
    /// </summary>
    [MaxLength(100)]
    public string CustomerSupplierName { get; set; } = string.Empty;

    /// <summary>
    /// 商品分類1（担当者コード）
    /// </summary>
    [MaxLength(15)]
    public string? ProductCategory1 { get; set; }

    /// <summary>
    /// 商品分類5
    /// </summary>
    [MaxLength(15)]
    public string? ProductCategory5 { get; set; }

    /// <summary>
    /// グループキー（商品＋荷印＋等級＋階級）
    /// </summary>
    [MaxLength(100)]
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>
    /// ソート用キー（商品分類1＋GroupKey＋日付）
    /// </summary>
    [MaxLength(150)]
    public string SortKey { get; set; } = string.Empty;

    // 集計用プロパティ
    /// <summary>
    /// 前日残高数量
    /// </summary>
    public decimal PreviousBalanceQuantity { get; set; }

    /// <summary>
    /// 前日残高金額
    /// </summary>
    public decimal PreviousBalanceAmount { get; set; }

    /// <summary>
    /// 仕入計数量
    /// </summary>
    public decimal TotalPurchaseQuantity { get; set; }

    /// <summary>
    /// 仕入計金額
    /// </summary>
    public decimal TotalPurchaseAmount { get; set; }

    /// <summary>
    /// 売上計数量
    /// </summary>
    public decimal TotalSalesQuantity { get; set; }

    /// <summary>
    /// 売上計金額
    /// </summary>
    public decimal TotalSalesAmount { get; set; }

    /// <summary>
    /// 当日残数量
    /// </summary>
    public decimal CurrentBalanceQuantity { get; set; }

    /// <summary>
    /// 当日残金額
    /// </summary>
    public decimal CurrentBalanceAmount { get; set; }

    /// <summary>
    /// 在庫単価
    /// </summary>
    public decimal InventoryUnitPrice { get; set; }

    /// <summary>
    /// 在庫金額
    /// </summary>
    public decimal InventoryAmount { get; set; }

    /// <summary>
    /// 粗利益合計
    /// </summary>
    public decimal TotalGrossProfit { get; set; }

    /// <summary>
    /// 粗利率（%）
    /// </summary>
    public decimal GrossProfitRate { get; set; }

    /// <summary>
    /// 歩引き金
    /// </summary>
    public decimal WalkingDiscount { get; set; }

    /// <summary>
    /// 月日表示用（MM/dd形式）
    /// </summary>
    public string MonthDayDisplay => TransactionDate.ToString("MM/dd");

    /// <summary>
    /// 負の値を▲付きで表示（単価以外）
    /// </summary>
    public string FormatNegativeValue(decimal value, bool isUnitPrice = false)
    {
        if (value < 0 && !isUnitPrice)
        {
            return $"{Math.Abs(value):N2}▲";
        }
        return value.ToString("N2");
    }

    /// <summary>
    /// 粗利率を%付きで表示
    /// </summary>
    public string FormatGrossProfitRate()
    {
        if (GrossProfitRate < 0)
        {
            return $"{Math.Abs(GrossProfitRate):N2}▲ %";
        }
        return $"{GrossProfitRate:N2} %";
    }

    /// <summary>
    /// グループキーを生成
    /// </summary>
    public void GenerateGroupKey()
    {
        GroupKey = $"{ProductCode}_{ShippingMarkCode}_{GradeCode}_{ClassCode}";
    }

    /// <summary>
    /// ソートキーを生成
    /// </summary>
    public void GenerateSortKey()
    {
        SortKey = $"{ProductCategory1 ?? "000"}_{GroupKey}_{TransactionDate:yyyyMMdd}_{VoucherNumber}";
    }

    /// <summary>
    /// 表示用区分名を取得
    /// </summary>
    public string GetDisplayCategory()
    {
        return (VoucherCategory, RecordType) switch
        {
            ("11", _) => "掛仕",
            ("12", _) => "現仕",
            ("51", _) => "掛売",
            ("52", _) => "現売",
            ("71", _) => "調整",
            (_, "Loss") => "腐り",
            (_, "Processing") => "加工",
            (_, "Transfer") => "振替",
            (_, "Previous") => "前残",
            _ => ""
        };
    }

    /// <summary>
    /// 商品分類5による例外処理判定
    /// </summary>
    public bool IsExceptionCase()
    {
        return ProductCategory5 == "99999";
    }
}