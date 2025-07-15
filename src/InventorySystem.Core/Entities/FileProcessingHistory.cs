namespace InventorySystem.Core.Entities;

/// <summary>
/// ファイル処理履歴エンティティ
/// </summary>
public class FileProcessingHistory
{
    /// <summary>
    /// ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// ファイル名
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// ファイルハッシュ
    /// </summary>
    public string FileHash { get; set; } = string.Empty;
    
    /// <summary>
    /// ファイルサイズ
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// 初回処理日時
    /// </summary>
    public DateTime FirstProcessedAt { get; set; }
    
    /// <summary>
    /// 最終処理日時
    /// </summary>
    public DateTime LastProcessedAt { get; set; }
    
    /// <summary>
    /// 総レコード数
    /// </summary>
    public int TotalRecordCount { get; set; }
    
    /// <summary>
    /// ファイル種別
    /// </summary>
    public string FileType { get; set; } = string.Empty;
    
    /// <summary>
    /// 日付別処理履歴（ナビゲーションプロパティ）
    /// </summary>
    public List<DateProcessingHistory> DateProcessingHistories { get; set; } = new();
}

/// <summary>
/// 日付別処理履歴エンティティ
/// </summary>
public class DateProcessingHistory
{
    /// <summary>
    /// ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// ファイル履歴ID
    /// </summary>
    public int FileHistoryId { get; set; }
    
    /// <summary>
    /// 処理対象日
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// 処理日時
    /// </summary>
    public DateTime ProcessedAt { get; set; }
    
    /// <summary>
    /// 処理レコード数
    /// </summary>
    public int RecordCount { get; set; }
    
    /// <summary>
    /// データセットID
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 処理種別
    /// </summary>
    public string ProcessType { get; set; } = string.Empty;
    
    /// <summary>
    /// 部門
    /// </summary>
    public string Department { get; set; } = string.Empty;
    
    /// <summary>
    /// 実行者
    /// </summary>
    public string ExecutedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// ファイル処理履歴（ナビゲーションプロパティ）
    /// </summary>
    public FileProcessingHistory? FileProcessingHistory { get; set; }
}