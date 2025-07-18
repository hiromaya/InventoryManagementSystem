using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// データセット管理リポジトリインターフェース
/// </summary>
public interface IDataSetManagementRepository
{
    /// <summary>
    /// データセットを作成
    /// </summary>
    Task<DataSetManagement> CreateAsync(DataSetManagement dataset);
    
    /// <summary>
    /// データセットIDで取得
    /// </summary>
    Task<DataSetManagement?> GetByIdAsync(string datasetId);
    
    /// <summary>
    /// 指定日付・処理種別の最新データセットを取得
    /// </summary>
    Task<DataSetManagement?> GetLatestByJobDateAndTypeAsync(DateTime jobDate, string processType);
    
    /// <summary>
    /// 指定日付のデータセット一覧を取得
    /// </summary>
    Task<IEnumerable<DataSetManagement>> GetByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// JobDateで有効なデータセットを取得
    /// </summary>
    Task<DataSetManagement?> GetActiveByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// データセットを更新
    /// </summary>
    Task<int> UpdateAsync(DataSetManagement dataset);
    
    /// <summary>
    /// データセットを無効化
    /// </summary>
    Task<int> DeactivateDataSetAsync(string dataSetId, string? deactivatedBy = null);
}