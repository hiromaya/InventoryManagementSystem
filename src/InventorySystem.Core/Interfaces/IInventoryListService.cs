using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 在庫表サービスインターフェース
/// </summary>
public interface IInventoryListService
{
    /// <summary>
    /// 在庫表データを取得
    /// </summary>
    /// <param name="reportDate">レポート日付</param>
    /// <returns>在庫表データリスト</returns>
    Task<List<InventoryListItem>> GetInventoryListDataAsync(DateTime reportDate);
    
    /// <summary>
    /// 担当者別在庫表データを取得
    /// </summary>
    /// <param name="reportDate">レポート日付</param>
    /// <returns>担当者別在庫表データ</returns>
    Task<List<InventoryListByStaff>> GetInventoryListByStaffAsync(DateTime reportDate);
    
    /// <summary>
    /// 在庫表処理を実行（CP在庫Mの作成・集計含む）
    /// </summary>
    /// <param name="reportDate">レポート日付</param>
    /// <returns>処理結果</returns>
    Task<InventoryListResult> ProcessInventoryListAsync(DateTime reportDate);
}

/// <summary>
/// 在庫表処理結果
/// </summary>
public class InventoryListResult
{
    /// <summary>
    /// 処理成功フラグ
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// データセットID
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 処理件数
    /// </summary>
    public int ProcessedCount { get; set; }
    
    /// <summary>
    /// 担当者別在庫表データ
    /// </summary>
    public List<InventoryListByStaff> StaffInventories { get; set; } = new();
    
    /// <summary>
    /// 全体合計
    /// </summary>
    public InventoryListTotal GrandTotal { get; set; } = new();
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 処理時間
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
}