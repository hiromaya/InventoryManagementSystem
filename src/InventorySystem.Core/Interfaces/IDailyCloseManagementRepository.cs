using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 日次終了管理リポジトリインターフェース
/// </summary>
public interface IDailyCloseManagementRepository
{
    /// <summary>
    /// 日次終了情報を作成
    /// </summary>
    Task<DailyCloseManagement> CreateAsync(DailyCloseManagement dailyClose);
    
    /// <summary>
    /// ジョブ日付で取得
    /// </summary>
    Task<DailyCloseManagement?> GetByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// 最新の日次終了情報を取得
    /// </summary>
    Task<DailyCloseManagement?> GetLatestAsync();
    
    /// <summary>
    /// ValidationStatusと備考を更新
    /// </summary>
    /// <param name="id">DailyCloseManagementのID</param>
    /// <param name="status">新しいValidationStatus</param>
    /// <param name="remark">追加備考</param>
    Task UpdateStatusAsync(int id, string status, string? remark = null);
}