namespace InventorySystem.Core.Entities;

/// <summary>
/// 監査ログエンティティ
/// </summary>
public class AuditLog
{
    /// <summary>
    /// ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 処理種別
    /// </summary>
    public string ProcessType { get; set; } = string.Empty;
    
    /// <summary>
    /// ジョブ日付
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// データセットID
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 実行者
    /// </summary>
    public string ExecutedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// 実行日時
    /// </summary>
    public DateTime ExecutedAt { get; set; }
    
    /// <summary>
    /// 結果（SUCCESS/FAILED）
    /// </summary>
    public string Result { get; set; } = string.Empty;
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 詳細情報（JSON形式）
    /// </summary>
    public string? Details { get; set; }
}