using InventorySystem.Core.Entities.Masters;

namespace InventorySystem.Core.Interfaces.Masters;

/// <summary>
/// 担当者マスタリポジトリインターフェース
/// </summary>
public interface IStaffMasterRepository
{
    /// <summary>
    /// すべての担当者を取得
    /// </summary>
    Task<IEnumerable<StaffMaster>> GetAllAsync();
    
    /// <summary>
    /// 担当者コードで取得
    /// </summary>
    Task<StaffMaster?> GetByCodeAsync(int staffCode);
    
    /// <summary>
    /// 部門コードで検索
    /// </summary>
    Task<IEnumerable<StaffMaster>> GetByDepartmentCodeAsync(int departmentCode);
    
    /// <summary>
    /// 分類コードで検索
    /// </summary>
    Task<IEnumerable<StaffMaster>> GetByCategoryCodeAsync(int categoryType, int categoryCode);
    
    /// <summary>
    /// 検索カナで検索
    /// </summary>
    Task<IEnumerable<StaffMaster>> SearchByKanaAsync(string searchKana);
    
    /// <summary>
    /// 担当者名で検索
    /// </summary>
    Task<IEnumerable<StaffMaster>> SearchByNameAsync(string staffName);
    
    /// <summary>
    /// 存在確認
    /// </summary>
    Task<bool> ExistsAsync(int staffCode);
    
    /// <summary>
    /// 一括挿入
    /// </summary>
    Task<int> InsertBulkAsync(IEnumerable<StaffMaster> staff);
    
    /// <summary>
    /// 更新
    /// </summary>
    Task<int> UpdateAsync(StaffMaster staff);
    
    /// <summary>
    /// 削除
    /// </summary>
    Task<int> DeleteAsync(int staffCode);
    
    /// <summary>
    /// すべて削除（テーブルクリア）
    /// </summary>
    Task<int> DeleteAllAsync();
    
    /// <summary>
    /// 挿入または更新（Upsert）
    /// </summary>
    Task<int> UpsertAsync(StaffMaster staff);
    
    /// <summary>
    /// 一括挿入または更新（Bulk Upsert）
    /// </summary>
    Task<int> UpsertBulkAsync(IEnumerable<StaffMaster> staff);
}