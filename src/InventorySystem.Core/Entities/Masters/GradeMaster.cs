using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Core.Entities.Masters;

/// <summary>
/// 等級マスタエンティティ
/// 販売大臣の等級汎用マスタに対応
/// </summary>
public class GradeMaster
{
    /// <summary>
    /// 等級コード（主キー）
    /// </summary>
    [Key]
    [Required]
    [MaxLength(15)]
    public string GradeCode { get; set; } = string.Empty;

    /// <summary>
    /// 等級名
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;

    /// <summary>
    /// 検索用カナ
    /// </summary>
    [MaxLength(100)]
    public string SearchKana { get; set; } = string.Empty;

    /// <summary>
    /// 汎用数値項目1
    /// </summary>
    public decimal? NumericValue1 { get; set; }

    /// <summary>
    /// 汎用数値項目2
    /// </summary>
    public decimal? NumericValue2 { get; set; }

    /// <summary>
    /// 汎用数値項目3
    /// </summary>
    public decimal? NumericValue3 { get; set; }

    /// <summary>
    /// 汎用数値項目4
    /// </summary>
    public decimal? NumericValue4 { get; set; }

    /// <summary>
    /// 汎用数値項目5
    /// </summary>
    public decimal? NumericValue5 { get; set; }

    /// <summary>
    /// 汎用日付項目1
    /// </summary>
    public DateTime? DateValue1 { get; set; }

    /// <summary>
    /// 汎用日付項目2
    /// </summary>
    public DateTime? DateValue2 { get; set; }

    /// <summary>
    /// 汎用日付項目3
    /// </summary>
    public DateTime? DateValue3 { get; set; }

    /// <summary>
    /// 汎用日付項目4
    /// </summary>
    public DateTime? DateValue4 { get; set; }

    /// <summary>
    /// 汎用日付項目5
    /// </summary>
    public DateTime? DateValue5 { get; set; }

    /// <summary>
    /// 汎用テキスト項目1
    /// </summary>
    [MaxLength(255)]
    public string? TextValue1 { get; set; }

    /// <summary>
    /// 汎用テキスト項目2
    /// </summary>
    [MaxLength(255)]
    public string? TextValue2 { get; set; }

    /// <summary>
    /// 汎用テキスト項目3
    /// </summary>
    [MaxLength(255)]
    public string? TextValue3 { get; set; }

    /// <summary>
    /// 汎用テキスト項目4
    /// </summary>
    [MaxLength(255)]
    public string? TextValue4 { get; set; }

    /// <summary>
    /// 汎用テキスト項目5
    /// </summary>
    [MaxLength(255)]
    public string? TextValue5 { get; set; }

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 文字列表現を取得
    /// </summary>
    public override string ToString()
    {
        return $"[{GradeCode}] {GradeName}";
    }
}