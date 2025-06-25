using CsvHelper.Configuration.Attributes;

namespace InventorySystem.Import.Models.Masters;

/// <summary>
/// 仕入先マスタCSVマッピングクラス
/// </summary>
public class SupplierMasterCsv
{
    [Name("仕入先コード")]
    [Index(0)]
    public string SupplierCode { get; set; } = string.Empty;

    [Name("仕入先名")]
    [Index(4)]
    public string SupplierName { get; set; } = string.Empty;

    [Name("仕入先名２")]
    [Index(2)]
    public string? SupplierName2 { get; set; }

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

    [Name("区分１")]
    [Index(11)]
    public string? SupplierCategory1 { get; set; }

    [Name("区分２")]
    [Index(12)]
    public string? SupplierCategory2 { get; set; }

    [Name("区分３")]
    [Index(13)]
    public string? SupplierCategory3 { get; set; }

    [Name("支払条件コード")]
    [Index(53)]
    public string? PaymentCode { get; set; }

    [Name("取引区分")]
    [Index(97)]
    public int? TransactionStatus { get; set; }

    /// <summary>
    /// EntityのIsActiveに変換
    /// </summary>
    public bool IsActive => TransactionStatus != 2; // 2:取引終了以外はアクティブ
}