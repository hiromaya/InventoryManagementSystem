using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventorySystem.Core.Interfaces.Development;

/// <summary>
/// 処理履歴サービスのインターフェース
/// </summary>
public interface IProcessingHistoryService
{
    /// <summary>
    /// ファイルが処理済みかチェック
    /// </summary>
    /// <param name="fileName">ファイル名</param>
    /// <param name="fileHash">ファイルハッシュ</param>
    /// <returns>処理済みの場合true</returns>
    Task<bool> IsFileProcessedAsync(string fileName, string fileHash);
    
    /// <summary>
    /// 指定日付・処理種別で処理済みかチェック
    /// </summary>
    /// <param name="fileName">ファイル名</param>
    /// <param name="jobDate">処理対象日</param>
    /// <param name="processType">処理種別</param>
    /// <param name="department">部門</param>
    /// <returns>処理済みの場合true</returns>
    Task<bool> IsDateProcessedAsync(string fileName, DateTime jobDate, string processType, string department = "DeptA");
    
    /// <summary>
    /// ファイル処理の記録
    /// </summary>
    /// <param name="fileName">ファイル名</param>
    /// <param name="fileHash">ファイルハッシュ</param>
    /// <param name="fileSize">ファイルサイズ</param>
    /// <param name="fileType">ファイル種別</param>
    /// <param name="totalRecordCount">総レコード数</param>
    /// <returns>ファイル履歴ID</returns>
    Task<int> RecordFileProcessingAsync(string fileName, string fileHash, long fileSize, string fileType, int totalRecordCount);
    
    /// <summary>
    /// 日付別処理の記録
    /// </summary>
    /// <param name="fileHistoryId">ファイル履歴ID</param>
    /// <param name="jobDate">処理対象日</param>
    /// <param name="recordCount">処理レコード数</param>
    /// <param name="datasetId">データセットID</param>
    /// <param name="processType">処理種別</param>
    /// <param name="department">部門</param>
    /// <param name="executedBy">実行者</param>
    /// <returns>日付処理履歴ID</returns>
    Task<int> RecordDateProcessingAsync(int fileHistoryId, DateTime jobDate, int recordCount, 
        string datasetId, string processType, string department = "DeptA", string executedBy = "System");
    
    /// <summary>
    /// 未処理日付の取得
    /// </summary>
    /// <param name="fileName">ファイル名</param>
    /// <param name="processType">処理種別</param>
    /// <param name="department">部門</param>
    /// <param name="startDate">開始日</param>
    /// <param name="endDate">終了日</param>
    /// <returns>未処理日付のリスト</returns>
    Task<List<DateTime>> GetUnprocessedDatesAsync(string fileName, string processType, string department = "DeptA", 
        DateTime? startDate = null, DateTime? endDate = null);
    
    /// <summary>
    /// ファイル履歴IDの取得（なければ作成）
    /// </summary>
    /// <param name="fileName">ファイル名</param>
    /// <param name="fileHash">ファイルハッシュ</param>
    /// <param name="fileSize">ファイルサイズ</param>
    /// <param name="fileType">ファイル種別</param>
    /// <param name="totalRecordCount">総レコード数</param>
    /// <returns>ファイル履歴ID</returns>
    Task<int> GetOrCreateFileHistoryAsync(string fileName, string fileHash, long fileSize, string fileType, int totalRecordCount);
    
    /// <summary>
    /// 処理履歴の削除（指定日数より古いもの）
    /// </summary>
    /// <param name="retentionDays">保持日数</param>
    /// <returns>削除された件数</returns>
    Task<int> CleanupOldHistoryAsync(int retentionDays = 90);
    
    /// <summary>
    /// ファイルの処理統計を取得
    /// </summary>
    /// <param name="fileName">ファイル名</param>
    /// <param name="startDate">開始日</param>
    /// <param name="endDate">終了日</param>
    /// <returns>処理統計</returns>
    Task<ProcessingStatistics> GetProcessingStatisticsAsync(string fileName, DateTime? startDate = null, DateTime? endDate = null);
}

/// <summary>
/// 処理統計
/// </summary>
public class ProcessingStatistics
{
    public string FileName { get; set; } = string.Empty;
    public int TotalDatesProcessed { get; set; }
    public int TotalRecordsProcessed { get; set; }
    public DateTime? FirstProcessedAt { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public List<string> ProcessTypes { get; set; } = new();
    public List<string> Departments { get; set; } = new();
}