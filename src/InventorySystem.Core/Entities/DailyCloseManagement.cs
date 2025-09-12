namespace InventorySystem.Core.Entities;

/// <summary>
/// 日次終了管理エンティティ
/// </summary>
public class DailyCloseManagement
{
    /// <summary>
    /// ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// ジョブ日付
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// データセットID
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 商品日報データセットID
    /// </summary>
    public string DailyReportDataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// バックアップパス
    /// </summary>
    public string? BackupPath { get; set; }
    
    /// <summary>
    /// 処理日時
    /// </summary>
    public DateTime ProcessedAt { get; set; }
    
    /// <summary>
    /// 処理者
    /// </summary>
    public string ProcessedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// データハッシュ値
    /// </summary>
    public string? DataHash { get; set; }

    /// <summary>
    /// 検証ステータス（PASSED/FAILED/WARNING）
    /// </summary>
    public string? ValidationStatus { get; set; }

    /// <summary>
    /// 備考（データ件数等の情報）
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// 備考にタイムスタンプ付きでメッセージを追記
    /// Gemini推奨：処理の追跡とデバッグのため
    /// </summary>
    /// <param name="message">追記するメッセージ</param>
    public void AppendRemark(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var newRemark = $"[{timestamp}] {message}";
        
        if (string.IsNullOrEmpty(Remarks))
        {
            Remarks = newRemark;
        }
        else
        {
            Remarks = $"{newRemark}{Environment.NewLine}{Remarks}";
        }
    }

    /// <summary>
    /// 処理時間情報を備考に追記
    /// </summary>
    /// <param name="startTime">処理開始時刻</param>
    /// <param name="additionalInfo">追加情報</param>
    public void AppendPerformanceInfo(DateTime startTime, string? additionalInfo = null)
    {
        var duration = DateTime.Now - startTime;
        var message = $"処理時間: {duration.TotalSeconds:F2}秒, 環境: {Environment.MachineName}";
        
        if (!string.IsNullOrEmpty(additionalInfo))
        {
            message += $", {additionalInfo}";
        }
        
        AppendRemark(message);
    }
}