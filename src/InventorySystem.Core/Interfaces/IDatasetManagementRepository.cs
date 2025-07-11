using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// データセット管理リポジトリインターフェース
/// </summary>
public interface IDatasetManagementRepository
{
    /// <summary>
    /// データセットを作成
    /// </summary>
    Task<DatasetManagement> CreateAsync(DatasetManagement dataset);
    
    /// <summary>
    /// データセットIDで取得
    /// </summary>
    Task<DatasetManagement?> GetByIdAsync(string datasetId);
    
    /// <summary>
    /// 指定日付・処理種別の最新データセットを取得
    /// </summary>
    Task<DatasetManagement?> GetLatestByJobDateAndTypeAsync(DateTime jobDate, string processType);
    
    /// <summary>
    /// 指定日付のデータセット一覧を取得
    /// </summary>
    Task<IEnumerable<DatasetManagement>> GetByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// JobDateで有効なデータセットを取得
    /// </summary>
    Task<DatasetManagement?> GetActiveByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// データセットを無効化
    /// </summary>
    Task<int> DeactivateDataSetAsync(string dataSetId, string? deactivatedBy = null);
}