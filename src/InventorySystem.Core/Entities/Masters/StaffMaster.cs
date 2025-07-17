namespace InventorySystem.Core.Entities.Masters;

/// <summary>
/// 担当者マスタエンティティ
/// </summary>
public class StaffMaster
{
    /// <summary>
    /// 担当者コード
    /// </summary>
    public int StaffCode { get; set; }
    
    /// <summary>
    /// 担当者名称
    /// </summary>
    public string StaffName { get; set; } = string.Empty;
    
    /// <summary>
    /// 検索カナ
    /// </summary>
    public string? SearchKana { get; set; }
    
    /// <summary>
    /// 分類1コード
    /// </summary>
    public int? Category1Code { get; set; }
    
    /// <summary>
    /// 分類2コード
    /// </summary>
    public int? Category2Code { get; set; }
    
    /// <summary>
    /// 分類3コード
    /// </summary>
    public int? Category3Code { get; set; }
    
    /// <summary>
    /// 部門コード
    /// </summary>
    public int? DepartmentCode { get; set; }
    
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}