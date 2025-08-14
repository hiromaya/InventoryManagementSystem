using CsvHelper.Configuration.Attributes;

namespace InventorySystem.Import.Models.Masters;

/// <summary>
/// 得意先マスタCSVマッピングクラス
/// </summary>
public class CustomerMasterCsv
{
    [Name("得意先コード")]
    [Index(0)]
    public string CustomerCode { get; set; } = string.Empty;

    [Name("得意先名")]
    [Index(4)]
    public string CustomerName { get; set; } = string.Empty;

    [Name("得意先名２")]
    [Index(2)]
    public string? CustomerName2 { get; set; }

    [Name("検索カナ")]
    [Index(3)]
    public string? SearchKana { get; set; }

    [Name("略称")]
    [Index(4)]
    public string? ShortName { get; set; }

    [Name("郵便番号")]
    [Index(5)]
    public string? PostalCode { get; set; }

    [Name("住所１")]
    [Index(6)]
    public string? Address1 { get; set; }

    [Name("住所２")]
    [Index(7)]
    public string? Address2 { get; set; }

    [Name("住所３")]
    [Index(8)]
    public string? Address3 { get; set; }

    [Name("電話番号")]
    [Index(9)]
    public string? PhoneNumber { get; set; }

    [Name("FAX番号")]
    [Index(10)]
    public string? FaxNumber { get; set; }

    [Name("取引先分類（営業日報ﾌｧｲﾙのKey）")]
    [Index(18)]
    public string? CustomerCategory1 { get; set; }

    // CustomerCategory2-5はCSVファイルに存在しないため削除
    // [Name("区分２")]
    // [Index(12)]
    // public string? CustomerCategory2 { get; set; }

    // [Name("区分３")]
    // [Index(13)]
    // public string? CustomerCategory3 { get; set; }

    // [Name("区分４")]
    // [Index(14)]
    // public string? CustomerCategory4 { get; set; }

    // [Name("区分５")]
    // [Index(15)]
    // public string? CustomerCategory5 { get; set; }

    [Name("汎用数値１")]
    [Index(16)]
    public decimal? WalkingRate { get; set; }

    [Name("請求先コード")]
    [Index(26)]
    public string? BillingCode { get; set; }

    [Name("取引区分")]
    [Index(97)]
    public int? TransactionStatus { get; set; }

    /// <summary>
    /// EntityのIsActiveに変換
    /// </summary>
    public bool IsActive => TransactionStatus != 2; // 2:取引終了以外はアクティブ
}