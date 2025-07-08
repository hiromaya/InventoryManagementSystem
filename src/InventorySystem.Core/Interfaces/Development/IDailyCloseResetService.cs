using System;
using System.Threading.Tasks;

namespace InventorySystem.Core.Interfaces.Development;

/// <summary>
/// 日次終了処理リセットサービスのインターフェース
/// </summary>
public interface IDailyCloseResetService
{
    /// <summary>
    /// 指定日付の日次終了処理をリセットする
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <param name="resetAll">在庫マスタも含めてリセットするかどうか</param>
    /// <returns>リセット結果</returns>
    Task<ResetResult> ResetDailyCloseAsync(DateTime jobDate, bool resetAll = false);
    
    /// <summary>
    /// 指定日付のリセットが可能か確認する
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>リセット可能な場合はtrue</returns>
    Task<bool> CanResetAsync(DateTime jobDate);
    
    /// <summary>
    /// 指定日付に関連するデータの状態を取得する
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>関連データの状態</returns>
    Task<RelatedDataStatus> GetRelatedDataStatusAsync(DateTime jobDate);
}

/// <summary>
/// リセット処理結果
/// </summary>
public class ResetResult
{
    public bool Success { get; set; }
    public int DeletedDailyCloseRecords { get; set; }
    public int DeletedProcessHistoryRecords { get; set; }
    public int ResetInventoryRecords { get; set; }
    public int DeletedAuditLogs { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.Now;
    
    public string GetSummary()
    {
        var summary = $"リセット結果: {(Success ? "成功" : "失敗")}\n";
        if (!string.IsNullOrEmpty(Message))
        {
            summary += $"メッセージ: {Message}\n";
        }
        summary += $"削除されたレコード:\n";
        summary += $"  - 日次終了管理: {DeletedDailyCloseRecords}件\n";
        summary += $"  - 処理履歴: {DeletedProcessHistoryRecords}件\n";
        summary += $"  - 監査ログ: {DeletedAuditLogs}件\n";
        if (ResetInventoryRecords > 0)
        {
            summary += $"  - リセットされた在庫: {ResetInventoryRecords}件\n";
        }
        return summary;
    }
}

/// <summary>
/// 関連データの状態
/// </summary>
public class RelatedDataStatus
{
    public bool HasDailyCloseRecord { get; set; }
    public bool HasProcessHistory { get; set; }
    public bool HasDailyReport { get; set; }
    public bool HasNextDayData { get; set; }
    public DateTime? LastDailyCloseAt { get; set; }
    public string? LastProcessedBy { get; set; }
}