using CsvHelper.Configuration.Attributes;

namespace InventorySystem.Import.Models.Masters;

/// <summary>
/// 商品マスタCSVマッピングクラス
/// </summary>
public class ProductMasterCsv
{
    [Name("商品コード")]
    [Index(0)]
    public string ProductCode { get; set; } = string.Empty;

    [Name("検索カナ")]
    [Index(1)]
    public string? SearchKana { get; set; }

    [Name("略称")]
    [Index(2)]
    public string? ShortName { get; set; }

    [Name("印刷用コード")]
    [Index(3)]
    public string? PrintCode { get; set; }

    [Name("商品名")]
    [Index(4)]
    public string ProductName { get; set; } = string.Empty;

    [Name("名称２")]
    [Index(5)]
    public string? ProductName2 { get; set; }

    [Name("名称３")]
    [Index(6)]
    public string? ProductName3 { get; set; }

    [Name("名称４")]
    [Index(7)]
    public string? ProductName4 { get; set; }

    [Name("名称５")]
    [Index(8)]
    public string? ProductName5 { get; set; }

    [Name("品合せ担当者CD")] 
    [Index(9)]
    public string? AssignmentStaffCode { get; set; }

    [Name("分類２コード")]
    [Index(10)]
    public string? Category2Code { get; set; }

    [Name("分類３コード")]
    [Index(11)]
    public string? Category3Code { get; set; }

    [Name("分類４コード")]
    [Index(12)]
    public string? Category4Code { get; set; }

    [Name("分類５コード")]
    [Index(13)]
    public string? Category5Code { get; set; }

    [Name("部門コード")]
    [Index(14)]
    public string? DepartmentCode { get; set; }

    [Name("バラ単位コード")]
    [Index(15)]
    public string? UnitCode { get; set; }

    [Name("ケース単位コード")]
    [Index(16)]
    public string? CaseUnitCode { get; set; }

    [Name("ケース２単位コード")]
    [Index(17)]
    public string? Case2UnitCode { get; set; }

    [Name("ケース入数")]
    [Index(18)]
    public decimal? CaseQuantity { get; set; }

    [Name("ケース２入数")]
    [Index(19)]
    public decimal? Case2Quantity { get; set; }

    [Name("バラ標準価格")]
    [Index(36)]
    public decimal? StandardPrice { get; set; }

    [Name("ケース標準価格")]
    [Index(37)]
    public decimal? CaseStandardPrice { get; set; }

    [Name("在庫管理")]
    [Index(56)]
    public int? StockManagedFlag { get; set; }

    [Name("消費税区分")]
    [Index(30)]  // 実際のインデックスは要確認
    public int? TaxType { get; set; }

    // =========================
    // データベース保存用のマッピング
    // =========================

    /// <summary>
    /// 商品分類1（担当者コード）へのマッピング
    /// 品合せ担当者CDをProductCategory1として使用
    /// </summary>
    public string? ProductCategory1 => AssignmentStaffCode;

    /// <summary>
    /// 商品分類2-5へのマッピング
    /// </summary>
    public string? ProductCategory2 => Category2Code;
    public string? ProductCategory3 => Category3Code;
    public string? ProductCategory4 => Category4Code;
    public string? ProductCategory5 => Category5Code;

    /// <summary>
    /// 在庫管理フラグをboolに変換
    /// </summary>
    public bool IsStockManaged => StockManagedFlag.HasValue && StockManagedFlag.Value == 1;

    /// <summary>
    /// 消費税率を取得（簡易実装）
    /// </summary>
    public int? GetTaxRate()
    {
        return TaxType switch
        {
            1 => 10,    // 標準税率
            2 => 8,     // 軽減税率
            3 => 0,     // 非課税
            _ => null
        };
    }
}