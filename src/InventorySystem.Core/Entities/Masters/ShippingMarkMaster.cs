namespace InventorySystem.Core.Entities.Masters;

/// <summary>
/// 荷印マスタエンティティ
/// </summary>
public class ShippingMarkMaster
{
    /// <summary>
    /// 荷印コード
    /// </summary>
    public required string ShippingMarkCode { get; set; }

    /// <summary>
    /// 荷印名
    /// </summary>
    public required string ManualShippingMark { get; set; }

    /// <summary>
    /// 検索カナ
    /// </summary>
    public string? SearchKana { get; set; }

    /// <summary>
    /// 汎用数値１
    /// </summary>
    public decimal? NumericValue1 { get; set; }

    /// <summary>
    /// 汎用数値２
    /// </summary>
    public decimal? NumericValue2 { get; set; }

    /// <summary>
    /// 汎用数値３
    /// </summary>
    public decimal? NumericValue3 { get; set; }

    /// <summary>
    /// 汎用数値４
    /// </summary>
    public decimal? NumericValue4 { get; set; }

    /// <summary>
    /// 汎用数値５
    /// </summary>
    public decimal? NumericValue5 { get; set; }

    /// <summary>
    /// 汎用日付１
    /// </summary>
    public DateTime? DateValue1 { get; set; }

    /// <summary>
    /// 汎用日付２
    /// </summary>
    public DateTime? DateValue2 { get; set; }

    /// <summary>
    /// 汎用日付３
    /// </summary>
    public DateTime? DateValue3 { get; set; }

    /// <summary>
    /// 汎用日付４
    /// </summary>
    public DateTime? DateValue4 { get; set; }

    /// <summary>
    /// 汎用日付５
    /// </summary>
    public DateTime? DateValue5 { get; set; }

    /// <summary>
    /// 汎用摘要１
    /// </summary>
    public string? TextValue1 { get; set; }

    /// <summary>
    /// 汎用摘要２
    /// </summary>
    public string? TextValue2 { get; set; }

    /// <summary>
    /// 汎用摘要３
    /// </summary>
    public string? TextValue3 { get; set; }

    /// <summary>
    /// 汎用摘要４
    /// </summary>
    public string? TextValue4 { get; set; }

    /// <summary>
    /// 汎用摘要５
    /// </summary>
    public string? TextValue5 { get; set; }
}