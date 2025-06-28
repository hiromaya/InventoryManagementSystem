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
    public decimal? GenericNumber1 { get; set; }

    [Name("汎用数値２")]
    [Index(4)]
    public decimal? GenericNumber2 { get; set; }

    [Name("汎用数値３")]
    [Index(5)]
    public decimal? GenericNumber3 { get; set; }

    [Name("汎用数値４")]
    [Index(6)]
    public decimal? GenericNumber4 { get; set; }

    [Name("汎用数値５")]
    [Index(7)]
    public decimal? GenericNumber5 { get; set; }

    [Name("汎用日付１")]
    [Index(8)]
    public string GenericDate1 { get; set; } = string.Empty;

    [Name("汎用日付２")]
    [Index(9)]
    public string GenericDate2 { get; set; } = string.Empty;

    [Name("汎用日付３")]
    [Index(10)]
    public string GenericDate3 { get; set; } = string.Empty;

    [Name("汎用日付４")]
    [Index(11)]
    public string GenericDate4 { get; set; } = string.Empty;

    [Name("汎用日付５")]
    [Index(12)]
    public string GenericDate5 { get; set; } = string.Empty;

    [Name("汎用摘要１")]
    [Index(13)]
    public string GenericNote1 { get; set; } = string.Empty;

    [Name("汎用摘要２")]
    [Index(14)]
    public string GenericNote2 { get; set; } = string.Empty;

    [Name("汎用摘要３")]
    [Index(15)]
    public string GenericNote3 { get; set; } = string.Empty;

    [Name("汎用摘要４")]
    [Index(16)]
    public string GenericNote4 { get; set; } = string.Empty;

    [Name("汎用摘要５")]
    [Index(17)]
    public string GenericNote5 { get; set; } = string.Empty;

    /// <summary>
    /// レコードが有効かどうかを判定
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(RegionCode) && 
               !string.IsNullOrWhiteSpace(RegionName);
    }
}