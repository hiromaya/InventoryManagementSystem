using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Core.Entities.Masters;

/// <summary>
/// 仕入先マスタ
/// </summary>
public class SupplierMaster
{
    /// <summary>
    /// 仕入先コード
    /// </summary>
    [Key]
    [Required]
    [MaxLength(15)]
    public string SupplierCode { get; set; } = string.Empty;

    /// <summary>
    /// 仕入先名
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>
    /// 仕入先名2
    /// </summary>
    [MaxLength(100)]
    public string? SupplierName2 { get; set; }

    /// <summary>
    /// 検索カナ
    /// </summary>
    [MaxLength(100)]
    public string? SearchKana { get; set; }

    /// <summary>
    /// 略称
    /// </summary>
    [MaxLength(50)]
    public string? ShortName { get; set; }

    /// <summary>
    /// 郵便番号
    /// </summary>
    [MaxLength(10)]
    public string? PostalCode { get; set; }

    /// <summary>
    /// 住所1
    /// </summary>
    [MaxLength(100)]
    public string? Address1 { get; set; }

    /// <summary>
    /// 住所2
    /// </summary>
    [MaxLength(100)]
    public string? Address2 { get; set; }

    /// <summary>
    /// 住所3
    /// </summary>
    [MaxLength(100)]
    public string? Address3 { get; set; }

    /// <summary>
    /// 電話番号
    /// </summary>
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// FAX番号
    /// </summary>
    [MaxLength(20)]
    public string? FaxNumber { get; set; }

    /// <summary>
    /// 仕入先分類1（'01'なら奨励金対象）
    /// </summary>
    [MaxLength(15)]
    public string? SupplierCategory1 { get; set; }

    /// <summary>
    /// 仕入先分類2
    /// </summary>
    [MaxLength(15)]
    public string? SupplierCategory2 { get; set; }

    /// <summary>
    /// 仕入先分類3
    /// </summary>
    [MaxLength(15)]
    public string? SupplierCategory3 { get; set; }

    /// <summary>
    /// 支払先コード
    /// </summary>
    [MaxLength(15)]
    public string? PaymentCode { get; set; }

    /// <summary>
    /// 取引区分（1:取引中、0:取引終了）
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 表示用の仕入先名を取得
    /// </summary>
    public string DisplayName => SupplierName2 ?? SupplierName;

    /// <summary>
    /// 住所を結合して取得
    /// </summary>
    public string FullAddress => string.Join(" ", new[] { Address1, Address2, Address3 }.Where(a => !string.IsNullOrWhiteSpace(a)));

    /// <summary>
    /// 奨励金対象かどうか
    /// </summary>
    public bool IsIncentiveTarget => SupplierCategory1 == "01";
}