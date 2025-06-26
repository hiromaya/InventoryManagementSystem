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
}