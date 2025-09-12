using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Reports.Models;

/// <summary>
/// 在庫表帳票用フラットデータ構造
/// C#側で完全制御されたデータモデル（FastReportは単純表示のみ）
/// </summary>
public class InventoryFlatRow
{
    /// <summary>
    /// 担当者コード
    /// </summary>
    [MaxLength(15)]
    public string StaffCode { get; set; } = string.Empty;

    /// <summary>
    /// 担当者名
    /// </summary>
    [MaxLength(50)]
    public string StaffName { get; set; } = string.Empty;

    /// <summary>
    /// 商品名
    /// </summary>
    [MaxLength(100)]
    public string Col1 { get; set; } = string.Empty;

    /// <summary>
    /// 荷印（荷印名表示）
    /// </summary>
    [MaxLength(100)]
    public string Col2 { get; set; } = string.Empty;

    /// <summary>
    /// 等級（等級名表示）
    /// </summary>
    [MaxLength(50)]
    public string Col3 { get; set; } = string.Empty;

    /// <summary>
    /// 階級（階級名表示）
    /// </summary>
    [MaxLength(50)]
    public string Col4 { get; set; } = string.Empty;

    /// <summary>
    /// 在庫数量（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string Col5 { get; set; } = string.Empty;

    /// <summary>
    /// 在庫単価（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string Col6 { get; set; } = string.Empty;

    /// <summary>
    /// 在庫金額（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string Col7 { get; set; } = string.Empty;

    /// <summary>
    /// 最終入荷日（フォーマット済み文字列）
    /// </summary>
    [MaxLength(20)]
    public string Col8 { get; set; } = string.Empty;

    /// <summary>
    /// 滞留マーク（!, !!, !!!）
    /// </summary>
    [MaxLength(10)]
    public string Col9 { get; set; } = string.Empty;

    // === 制御用フィールド ===

    /// <summary>
    /// 行種別（RowTypes定数を使用）
    /// </summary>
    [MaxLength(20)]
    public string RowType { get; set; } = InventoryRowTypes.Detail;

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

    // === FastReport用制御フラグ ===

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

    // === 計算用元データ（小計計算時に使用） ===

    /// <summary>
    /// 在庫数量（計算用）
    /// </summary>
    public decimal StockQuantity { get; set; } = 0;

    /// <summary>
    /// 在庫金額（計算用）
    /// </summary>
    public decimal StockAmount { get; set; } = 0;

    /// <summary>
    /// 商品コード（グループ化用）
    /// </summary>
    [MaxLength(20)]
    public string ProductCode { get; set; } = string.Empty;

    // === ページ番号制御 ===

    /// <summary>
    /// ページ番号（第二フェーズで設定）
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// 総ページ数（第二フェーズで設定）
    /// </summary>
    public int TotalPages { get; set; } = 1;

    /// <summary>
    /// ページグループ（担当者別ページ管理用）
    /// </summary>
    [MaxLength(50)]
    public string PageGroup { get; set; } = string.Empty;
}

/// <summary>
/// 在庫表行種別定数
/// </summary>
public static class InventoryRowTypes
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
    /// 担当者別合計見出し
    /// </summary>
    public const string StaffTotalHeader = "STAFF_TOTAL_HEADER";

    /// <summary>
    /// 担当者別合計
    /// </summary>
    public const string StaffTotal = "STAFF_TOTAL";

    /// <summary>
    /// 空行
    /// </summary>
    public const string BlankLine = "BLANK";
    
    /// <summary>
    /// 改ページ用ダミー行
    /// </summary>
    public const string PageBreak = "PAGE_BREAK";
    
    /// <summary>
    /// ページ埋め用ダミー行（空行）
    /// </summary>
    public const string Dummy = "DUMMY";
}