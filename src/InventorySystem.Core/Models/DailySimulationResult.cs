namespace InventorySystem.Core.Models;

/// <summary>
/// 日次処理シミュレーション結果
/// </summary>
public class DailySimulationResult
{
    /// <summary>
    /// 処理対象日
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// 部門
    /// </summary>
    public string Department { get; set; } = string.Empty;
    
    /// <summary>
    /// 開始時刻
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 終了時刻
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// 処理時間
    /// </summary>
    public TimeSpan ProcessingTime => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    
    /// <summary>
    /// 成功フラグ
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// ドライランモード
    /// </summary>
    public bool IsDryRun { get; set; }
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 処理ステップ結果
    /// </summary>
    public List<SimulationStepResult> StepResults { get; set; } = new();
    
    /// <summary>
    /// 処理統計
    /// </summary>
    public SimulationStatistics Statistics { get; set; } = new();
    
    /// <summary>
    /// 生成されたファイル
    /// </summary>
    public List<string> GeneratedFiles { get; set; } = new();
}

/// <summary>
/// シミュレーションステップ結果
/// </summary>
public class SimulationStepResult
{
    /// <summary>
    /// ステップ番号
    /// </summary>
    public int StepNumber { get; set; }
    
    /// <summary>
    /// ステップ名
    /// </summary>
    public string StepName { get; set; } = string.Empty;
    
    /// <summary>
    /// 成功フラグ
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 開始時刻
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 終了時刻
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// 処理時間
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    
    /// <summary>
    /// メッセージ
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// 詳細情報
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// シミュレーション統計
/// </summary>
public class SimulationStatistics
{
    /// <summary>
    /// インポート統計
    /// </summary>
    public ImportStatistics Import { get; set; } = new();
    
    /// <summary>
    /// アンマッチ統計
    /// </summary>
    public UnmatchStatistics Unmatch { get; set; } = new();
    
    /// <summary>
    /// 商品日報統計
    /// </summary>
    public ReportStatistics DailyReport { get; set; } = new();
    
    /// <summary>
    /// 在庫表統計
    /// </summary>
    public ReportStatistics InventoryList { get; set; } = new();
    
    /// <summary>
    /// 日次終了統計
    /// </summary>
    public DailyCloseStatistics DailyClose { get; set; } = new();
}

/// <summary>
/// インポート統計
/// </summary>
public class ImportStatistics
{
    /// <summary>
    /// 処理ファイル数
    /// </summary>
    public int ProcessedFiles { get; set; }
    
    /// <summary>
    /// 新規レコード数
    /// </summary>
    public int NewRecords { get; set; }
    
    /// <summary>
    /// スキップレコード数
    /// </summary>
    public int SkippedRecords { get; set; }
    
    /// <summary>
    /// エラーレコード数
    /// </summary>
    public int ErrorRecords { get; set; }
    
    /// <summary>
    /// ファイル別統計
    /// </summary>
    public Dictionary<string, FileImportStatistics> FileStatistics { get; set; } = new();
}

/// <summary>
/// ファイルインポート統計
/// </summary>
public class FileImportStatistics
{
    /// <summary>
    /// ファイル名
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// 新規レコード数
    /// </summary>
    public int NewRecords { get; set; }
    
    /// <summary>
    /// スキップレコード数
    /// </summary>
    public int SkippedRecords { get; set; }
    
    /// <summary>
    /// エラーレコード数
    /// </summary>
    public int ErrorRecords { get; set; }
    
    /// <summary>
    /// ファイルサイズ
    /// </summary>
    public long FileSize { get; set; }
}

/// <summary>
/// アンマッチ統計
/// </summary>
public class UnmatchStatistics
{
    /// <summary>
    /// アンマッチ件数
    /// </summary>
    public int UnmatchCount { get; set; }
    
    /// <summary>
    /// アンマッチリストファイルパス
    /// </summary>
    public string? UnmatchListPath { get; set; }
}

/// <summary>
/// レポート統計
/// </summary>
public class ReportStatistics
{
    /// <summary>
    /// レポートファイルパス
    /// </summary>
    public string? ReportPath { get; set; }
    
    /// <summary>
    /// データ件数
    /// </summary>
    public int DataCount { get; set; }
    
    /// <summary>
    /// ページ数
    /// </summary>
    public int PageCount { get; set; }
}

/// <summary>
/// 日次終了統計
/// </summary>
public class DailyCloseStatistics
{
    /// <summary>
    /// 更新された在庫マスタ件数
    /// </summary>
    public int UpdatedInventoryCount { get; set; }
    
    /// <summary>
    /// バックアップファイルパス
    /// </summary>
    public string? BackupPath { get; set; }
    
    /// <summary>
    /// データハッシュ
    /// </summary>
    public string? DataHash { get; set; }
}