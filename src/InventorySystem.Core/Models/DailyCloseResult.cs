using System;

namespace InventorySystem.Core.Models;

/// <summary>
/// 日次終了処理結果
/// </summary>
public class DailyCloseResult
{
    /// <summary>
    /// 処理対象日
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// データセットID
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 処理開始時刻
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 処理終了時刻
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// 処理時間
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
    
    /// <summary>
    /// 処理成功フラグ
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 更新された在庫マスタ件数
    /// </summary>
    public int UpdatedInventoryCount { get; set; }
    
    /// <summary>
    /// 非アクティブ化された在庫件数
    /// </summary>
    public int DeactivatedCount { get; set; }
    
    /// <summary>
    /// バックアップパス
    /// </summary>
    public string? BackupPath { get; set; }
    
    /// <summary>
    /// データハッシュ
    /// </summary>
    public string? DataHash { get; set; }
    
    /// <summary>
    /// 開発モードフラグ
    /// </summary>
    public bool IsDevelopmentMode { get; set; }
    
    /// <summary>
    /// 結果の概要を取得
    /// </summary>
    public string GetSummary()
    {
        var summary = $"日次終了処理結果 - {JobDate:yyyy-MM-dd}\n";
        summary += $"ステータス: {(Success ? "成功" : "失敗")}\n";
        
        if (IsDevelopmentMode)
        {
            summary += "モード: 開発環境\n";
        }
        
        if (!string.IsNullOrEmpty(DataSetId))
        {
            summary += $"データセットID: {DataSetId}\n";
        }
        
        summary += $"処理時間: {ProcessingTime.TotalSeconds:F1}秒\n";
        summary += $"更新在庫数: {UpdatedInventoryCount:N0}件\n";
        
        if (DeactivatedCount > 0)
        {
            summary += $"非アクティブ化: {DeactivatedCount:N0}件\n";
        }
        
        if (!string.IsNullOrEmpty(BackupPath))
        {
            summary += $"バックアップ: {BackupPath}\n";
        }
        
        if (!Success && !string.IsNullOrEmpty(ErrorMessage))
        {
            summary += $"エラー: {ErrorMessage}\n";
        }
        
        return summary;
    }
}