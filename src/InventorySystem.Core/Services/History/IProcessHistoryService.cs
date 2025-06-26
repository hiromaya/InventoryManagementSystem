using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Services.History;

/// <summary>
/// 処理履歴サービスインターフェース
/// </summary>
public interface IProcessHistoryService
{
    /// <summary>
    /// 処理を開始
    /// </summary>
    /// <param name="datasetId">データセットID</param>
    /// <param name="jobDate">ジョブ日付</param>
    /// <param name="processType">処理種別</param>
    /// <param name="executedBy">実行者</param>
    /// <returns>処理履歴</returns>
    Task<ProcessHistory> StartProcess(string datasetId, DateTime jobDate, string processType, string executedBy = "System");
    
    /// <summary>
    /// 処理を完了
    /// </summary>
    /// <param name="historyId">履歴ID</param>
    /// <param name="success">成功フラグ</param>
    /// <param name="message">メッセージ（エラーメッセージ等）</param>
    Task CompleteProcess(int historyId, bool success, string? message = null);
    
    /// <summary>
    /// 最後に成功した処理を取得
    /// </summary>
    /// <param name="processType">処理種別</param>
    /// <returns>処理履歴</returns>
    Task<ProcessHistory?> GetLastSuccessfulProcess(string processType);
    
    /// <summary>
    /// 完了メールを送信（日次終了処理のみ）
    /// </summary>
    /// <param name="history">処理履歴</param>
    Task SendCompletionEmail(ProcessHistory history);
    
    /// <summary>
    /// 処理履歴を取得
    /// </summary>
    /// <param name="jobDate">ジョブ日付</param>
    /// <param name="processType">処理種別</param>
    /// <returns>処理履歴一覧</returns>
    Task<IEnumerable<ProcessHistory>> GetProcessHistory(DateTime jobDate, string processType);
}