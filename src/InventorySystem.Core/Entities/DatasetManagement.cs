namespace InventorySystem.Core.Entities;

/// <summary>
/// データセット管理エンティティ
/// </summary>
public class DatasetManagement
{
    /// <summary>
    /// データセットID
    /// </summary>
    public string DatasetId { get; set; } = string.Empty;
    
    /// <summary>
    /// ジョブ日付
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// 処理種別
    /// </summary>
    public string ProcessType { get; set; } = string.Empty;
    
    /// <summary>
    /// インポートファイル一覧（JSON形式）
    /// </summary>
    public string? ImportedFiles { get; set; }
    
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 作成者
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// 処理履歴（ナビゲーションプロパティ）
    /// </summary>
    public ICollection<ProcessHistory> ProcessHistories { get; set; } = new List<ProcessHistory>();
}