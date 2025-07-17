using InventorySystem.Core.Entities.Masters;

namespace InventorySystem.Core.Interfaces.Masters;

/// <summary>
/// 単位マスタリポジトリインターフェース
/// </summary>
public interface IUnitMasterRepository
{
    /// <summary>
    /// すべての単位を取得
    /// </summary>
    Task<IEnumerable<UnitMaster>> GetAllAsync();
    
    /// <summary>
    /// 単位コードで取得
    /// </summary>
    Task<UnitMaster?> GetByCodeAsync(int unitCode);
    
    /// <summary>
    /// 検索カナで検索
    /// </summary>
    Task<IEnumerable<UnitMaster>> SearchByKanaAsync(string searchKana);
    
    /// <summary>
    /// 単位名で検索
    /// </summary>
    Task<IEnumerable<UnitMaster>> SearchByNameAsync(string unitName);
    
    /// <summary>
    /// 存在確認
    /// </summary>
    Task<bool> ExistsAsync(int unitCode);
    
    /// <summary>
    /// 一括挿入
    /// </summary>
    Task<int> InsertBulkAsync(IEnumerable<UnitMaster> units);
    
    /// <summary>
    /// 更新
    /// </summary>
    Task<int> UpdateAsync(UnitMaster unit);
    
    /// <summary>
    /// 削除
    /// </summary>
    Task<int> DeleteAsync(int unitCode);
    
    /// <summary>
    /// すべて削除（テーブルクリア）
    /// </summary>
    Task<int> DeleteAllAsync();
    
    /// <summary>
    /// 挿入または更新（Upsert）
    /// </summary>
    Task<int> UpsertAsync(UnitMaster unit);
    
    /// <summary>
    /// 一括挿入または更新（Bulk Upsert）
    /// </summary>
    Task<int> UpsertBulkAsync(IEnumerable<UnitMaster> units);
}