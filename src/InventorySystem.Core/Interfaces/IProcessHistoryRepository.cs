using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 処理履歴リポジトリインターフェース
/// </summary>
public interface IProcessHistoryRepository
{
    /// <summary>
    /// 処理履歴を作成
    /// </summary>
    Task<ProcessHistory> CreateAsync(ProcessHistory history);
    
    /// <summary>
    /// 処理履歴を更新
    /// </summary>
    Task UpdateAsync(ProcessHistory history);
    
    /// <summary>
    /// 指定日付・処理種別の履歴を取得
    /// </summary>
    Task<IEnumerable<ProcessHistory>> GetByJobDateAndTypeAsync(DateTime jobDate, string processType);
    
    /// <summary>
    /// 最後に成功した処理を取得
    /// </summary>
    Task<ProcessHistory?> GetLastSuccessfulAsync(string processType);
    
    /// <summary>
    /// IDで取得
    /// </summary>
    Task<ProcessHistory?> GetByIdAsync(int id);
    
    /// <summary>
    /// データセットIDで取得
    /// </summary>
    Task<ProcessHistory?> GetByDatasetIdAsync(string datasetId);
}