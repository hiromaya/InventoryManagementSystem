using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Reports.Models;

/// <summary>
/// 商品勘定帳票用フラットデータ構造
/// C#側で完全制御されたデータモデル（FastReportは単純表示のみ）
/// </summary>
public class ProductAccountFlatRow
{
    /// <summary>
    /// 担当者コード
    /// </summary>
    [MaxLength(15)]
    public string ProductCategory1 { get; set; } = string.Empty;

    /// <summary>
    /// 担当者名
    /// </summary>
    [MaxLength(50)]
    public string ProductCategory1Name { get; set; } = string.Empty;

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
    [MaxLength(10)]
    public string ShippingMarkCode { get; set; } = string.Empty;

    /// <summary>
    /// 手入力荷印（8文字固定）
    /// </summary>
    [MaxLength(8)]
    public string ManualShippingMark { get; set; } = string.Empty;

    /// <summary>
    /// 荷印名（荷印マスタから取得した名称）
    /// </summary>
    [MaxLength(100)]
    public string ShippingMarkName { get; set; } = string.Empty;

    /// <summary>
    /// 等級名
    /// </summary>
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;

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
    /// 画面表示用区分（前残、掛仕、掛売等）
    /// </summary>
    [MaxLength(10)]
    public string DisplayCategory { get; set; } = string.Empty;

    /// <summary>
    /// 月/日表示（MM/dd形式）
    /// </summary>
    [MaxLength(10)]
    public string MonthDay { get; set; } = string.Empty;

    /// <summary>
    /// 仕入数量（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string PurchaseQuantity { get; set; } = string.Empty;

    /// <summary>
    /// 売上数量（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string SalesQuantity { get; set; } = string.Empty;

    /// <summary>
    /// 残数量（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string RemainingQuantity { get; set; } = string.Empty;

    /// <summary>
    /// 単価（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string UnitPrice { get; set; } = string.Empty;

    /// <summary>
    /// 金額（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string Amount { get; set; } = string.Empty;

    /// <summary>
    /// 粗利益（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string GrossProfit { get; set; } = string.Empty;

    /// <summary>
    /// 取引先名（得意先または仕入先）
    /// </summary>
    [MaxLength(100)]
    public string CustomerSupplierName { get; set; } = string.Empty;

    // === 制御用フィールド ===

    /// <summary>
    /// 行種別（RowTypes定数を使用）
    /// </summary>
    [MaxLength(20)]
    public string RowType { get; set; } = RowTypes.Detail;

    /// <summary>
    /// 表示順序
    /// </summary>
    public int RowSequence { get; set; } = 0;

    /// <summary>
    /// 改ページフラグ（担当者変更時true）
    /// </summary>
    public bool IsPageBreak { get; set; } = false;

    /// <summary>
    /// 灰色背景フラグ（グループヘッダー時true）
    /// </summary>
    public bool IsGrayBackground { get; set; } = false;

    /// <summary>
    /// 太字フラグ（ヘッダー・小計時true）
    /// </summary>
    public bool IsBold { get; set; } = false;

    /// <summary>
    /// インデントレベル（将来拡張用）
    /// </summary>
    public int IndentLevel { get; set; } = 0;

    // === 集計用フィールド ===

    /// <summary>
    /// 小計行フラグ
    /// </summary>
    public bool IsSubtotal { get; set; } = false;


    /// <summary>
    /// 小計ラベル（商品別小計、担当者別合計等）
    /// </summary>
    [MaxLength(50)]
    public string SubtotalLabel { get; set; } = string.Empty;

    /// <summary>
    /// FastReport用の改ページ制御（"1"または"0"）
    /// </summary>
    public string PageBreakFlag => IsPageBreak ? "1" : "0";

    /// <summary>
    /// FastReport用の灰色背景制御（"1"または"0"）
    /// </summary>
    public string GrayBackgroundFlag => IsGrayBackground ? "1" : "0";

    /// <summary>
    /// FastReport用の太字制御（"1"または"0"）
    /// </summary>
    public string BoldFlag => IsBold ? "1" : "0";
}

/// <summary>
/// 行種別定数
/// </summary>
public static class RowTypes
{
    /// <summary>
    /// 担当者ヘッダー（改ページ用）
    /// </summary>
    public const string StaffHeader = "STAFF_HEADER";

    /// <summary>
    /// 商品グループヘッダー（灰色背景）
    /// </summary>
    public const string ProductGroupHeader = "PRODUCT_GROUP";

    /// <summary>
    /// 明細行
    /// </summary>
    public const string Detail = "DETAIL";

    /// <summary>
    /// 商品別小計見出し
    /// </summary>
    public const string ProductSubtotalHeader = "PRODUCT_SUBTOTAL_HEADER";
    
    /// <summary>
    /// 商品別小計
    /// </summary>
    public const string ProductSubtotal = "PRODUCT_SUBTOTAL";


    /// <summary>
    /// 空行
    /// </summary>
    public const string BlankLine = "BLANK";
    
    /// <summary>
    /// 35行改ページ用ダミー行
    /// </summary>
    public const string PageBreak = "PAGE_BREAK";
    
    /// <summary>
    /// ページ埋め用ダミー行（空行）
    /// </summary>
    public const string Dummy = "DUMMY";
}