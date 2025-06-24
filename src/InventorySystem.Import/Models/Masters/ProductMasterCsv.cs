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

    [Name("商品名称１")]
    [Index(1)]
    public string ProductName { get; set; } = string.Empty;

    [Name("名称２")]
    [Index(2)]
    public string? ProductName2 { get; set; }

    [Name("名称３")]
    [Index(3)]
    public string? ProductName3 { get; set; }

    [Name("名称４")]
    [Index(4)]
    public string? ProductName4 { get; set; }

    [Name("名称５")]
    [Index(5)]
    public string? ProductName5 { get; set; }

    [Name("検索カナ")]
    [Index(6)]
    public string? SearchKana { get; set; }

    [Name("略称")]
    [Index(7)]
    public string? ShortName { get; set; }

    [Name("印刷用コード")]
    [Index(8)]
    public string? PrintCode { get; set; }

    [Name("区分１")]
    [Index(9)]
    public string? ProductCategory1 { get; set; }

    [Name("区分２")]
    [Index(10)]
    public string? ProductCategory2 { get; set; }

    [Name("区分３")]
    [Index(11)]
    public string? ProductCategory3 { get; set; }

    [Name("区分４")]
    [Index(12)]
    public string? ProductCategory4 { get; set; }

    [Name("区分５")]
    [Index(13)]
    public string? ProductCategory5 { get; set; }

    [Name("バラ単位")]
    [Index(15)]
    public string? UnitCode { get; set; }

    [Name("ケース単位")]
    [Index(16)]
    public string? CaseUnitCode { get; set; }

    [Name("ケース２単位")]
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

    [Name("在庫管理フラグ")]
    [Index(70)]
    public int? StockManagedFlag { get; set; }

    [Name("消費税区分")]
    [Index(71)]
    public int? TaxType { get; set; }

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