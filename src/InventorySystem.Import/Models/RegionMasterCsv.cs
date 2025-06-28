using CsvHelper.Configuration.Attributes;

namespace InventorySystem.Import.Models;

/// <summary>
/// 産地汎用マスタCSVマッピングクラス
/// </summary>
public class RegionMasterCsv
{
    [Name("産地コード")]
    [Index(0)]
    public string RegionCode { get; set; } = string.Empty;

    [Name("産地名")]
    [Index(1)]
    public string RegionName { get; set; } = string.Empty;

    [Name("検索カナ")]
    [Index(2)]
    public string SearchKana { get; set; } = string.Empty;

    [Name("汎用数値１")]
    [Index(3)]
    public decimal? NumericValue1 { get; set; }

    [Name("汎用数値２")]
    [Index(4)]
    public decimal? NumericValue2 { get; set; }

    [Name("汎用数値３")]
    [Index(5)]
    public decimal? NumericValue3 { get; set; }

    [Name("汎用数値４")]
    [Index(6)]
    public decimal? NumericValue4 { get; set; }

    [Name("汎用数値５")]
    [Index(7)]
    public decimal? NumericValue5 { get; set; }

    [Name("汎用日付１")]
    [Index(8)]
    public DateTime? DateValue1 { get; set; }

    [Name("汎用日付２")]
    [Index(9)]
    public DateTime? DateValue2 { get; set; }

    [Name("汎用日付３")]
    [Index(10)]
    public DateTime? DateValue3 { get; set; }

    [Name("汎用日付４")]
    [Index(11)]
    public DateTime? DateValue4 { get; set; }

    [Name("汎用日付５")]
    [Index(12)]
    public DateTime? DateValue5 { get; set; }

    [Name("汎用摘要１")]
    [Index(13)]
    public string? TextValue1 { get; set; }

    [Name("汎用摘要２")]
    [Index(14)]
    public string? TextValue2 { get; set; }

    [Name("汎用摘要３")]
    [Index(15)]
    public string? TextValue3 { get; set; }

    [Name("汎用摘要４")]
    [Index(16)]
    public string? TextValue4 { get; set; }

    [Name("汎用摘要５")]
    [Index(17)]
    public string? TextValue5 { get; set; }

    /// <summary>
    /// レコードが有効かどうかを判定
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(RegionCode) && 
               !string.IsNullOrWhiteSpace(RegionName);
    }
}