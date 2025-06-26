namespace InventorySystem.Core.Entities;

/// <summary>
/// 処理履歴エンティティ
/// </summary>
public class ProcessHistory
{
    /// <summary>
    /// ID
    /// </summary>
    public int Id { get; set; }
    
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
    /// 開始時刻
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 終了時刻
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// ステータス
    /// </summary>
    public ProcessStatus Status { get; set; }
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 実行者
    /// </summary>
    public string ExecutedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// データセット管理（ナビゲーションプロパティ）
    /// </summary>
    public DatasetManagement? Dataset { get; set; }
}

/// <summary>
/// 処理ステータス列挙型
/// </summary>
public enum ProcessStatus
{
    /// <summary>
    /// 実行中
    /// </summary>
    Running = 1,
    
    /// <summary>
    /// 完了
    /// </summary>
    Completed = 2,
    
    /// <summary>
    /// 失敗
    /// </summary>
    Failed = 3,
    
    /// <summary>
    /// キャンセル
    /// </summary>
    Cancelled = 4
}