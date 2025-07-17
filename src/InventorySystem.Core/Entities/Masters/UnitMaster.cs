namespace InventorySystem.Core.Entities.Masters;

/// <summary>
/// 単位マスタエンティティ
/// </summary>
public class UnitMaster
{
    /// <summary>
    /// 単位コード
    /// </summary>
    public int UnitCode { get; set; }
    
    /// <summary>
    /// 単位名称
    /// </summary>
    public string UnitName { get; set; } = string.Empty;
    
    /// <summary>
    /// 検索カナ
    /// </summary>
    public string? SearchKana { get; set; }
    
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}