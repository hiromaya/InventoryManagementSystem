using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Core.Entities.Masters;

/// <summary>
/// 商品マスタ
/// </summary>
public class ProductMaster
{
    /// <summary>
    /// 商品コード
    /// </summary>
    [Key]
    [Required]
    [MaxLength(15)]
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// 商品名
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 商品名2
    /// </summary>
    [MaxLength(100)]
    public string? ProductName2 { get; set; }

    /// <summary>
    /// 商品名3
    /// </summary>
    [MaxLength(100)]
    public string? ProductName3 { get; set; }

    /// <summary>
    /// 商品名4
    /// </summary>
    [MaxLength(100)]
    public string? ProductName4 { get; set; }

    /// <summary>
    /// 商品名5
    /// </summary>
    [MaxLength(100)]
    public string? ProductName5 { get; set; }

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
    /// 印刷用コード
    /// </summary>
    [MaxLength(20)]
    public string? PrintCode { get; set; }

    /// <summary>
    /// 商品分類1コード（担当者）
    /// </summary>
    [MaxLength(15)]
    public string? ProductCategory1 { get; set; }

    /// <summary>
    /// 商品分類2コード
    /// </summary>
    [MaxLength(15)]
    public string? ProductCategory2 { get; set; }

    /// <summary>
    /// 商品分類3コード
    /// </summary>
    [MaxLength(15)]
    public string? ProductCategory3 { get; set; }

    /// <summary>
    /// 商品分類4コード
    /// </summary>
    [MaxLength(15)]
    public string? ProductCategory4 { get; set; }

    /// <summary>
    /// 商品分類5コード
    /// </summary>
    [MaxLength(15)]
    public string? ProductCategory5 { get; set; }

    /// <summary>
    /// バラ単位コード
    /// </summary>
    [MaxLength(10)]
    public string? UnitCode { get; set; }

    /// <summary>
    /// ケース単位コード
    /// </summary>
    [MaxLength(10)]
    public string? CaseUnitCode { get; set; }

    /// <summary>
    /// ケース2単位コード
    /// </summary>
    [MaxLength(10)]
    public string? Case2UnitCode { get; set; }

    /// <summary>
    /// ケース入数
    /// </summary>
    public decimal? CaseQuantity { get; set; }

    /// <summary>
    /// ケース2入数
    /// </summary>
    public decimal? Case2Quantity { get; set; }

    /// <summary>
    /// バラ標準価格
    /// </summary>
    public decimal? StandardPrice { get; set; }

    /// <summary>
    /// ケース標準価格
    /// </summary>
    public decimal? CaseStandardPrice { get; set; }

    /// <summary>
    /// 在庫管理フラグ
    /// </summary>
    public bool IsStockManaged { get; set; } = true;

    /// <summary>
    /// 消費税率
    /// </summary>
    public int? TaxRate { get; set; }

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 表示用の商品名を取得
    /// </summary>
    public string DisplayName => ProductName2 ?? ProductName;

    /// <summary>
    /// 商品名の全バリエーションを配列で取得
    /// </summary>
    public string[] AllProductNames => new[] { ProductName, ProductName2, ProductName3, ProductName4, ProductName5 }
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Select(n => n!)
        .ToArray();
}