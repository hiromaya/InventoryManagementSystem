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
    public string DatasetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 商品日報データセットID
    /// </summary>
    public string DailyReportDatasetId { get; set; } = string.Empty;
    
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
}