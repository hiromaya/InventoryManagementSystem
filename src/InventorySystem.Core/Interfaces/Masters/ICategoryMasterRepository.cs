using InventorySystem.Core.Entities.Masters;

namespace InventorySystem.Core.Interfaces.Masters;

/// <summary>
/// 分類マスタリポジトリの汎用インターフェース
/// </summary>
/// <typeparam name="T">分類マスタエンティティタイプ</typeparam>
public interface ICategoryMasterRepository<T> where T : CategoryMasterBase
{
    /// <summary>
    /// すべての分類を取得
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync();
    
    /// <summary>
    /// コードで取得
    /// </summary>
    Task<T?> GetByCodeAsync(int categoryCode);
    
    /// <summary>
    /// 検索カナで検索
    /// </summary>
    Task<IEnumerable<T>> SearchByKanaAsync(string searchKana);
    
    /// <summary>
    /// 名称で検索
    /// </summary>
    Task<IEnumerable<T>> SearchByNameAsync(string categoryName);
    
    /// <summary>
    /// 存在確認
    /// </summary>
    Task<bool> ExistsAsync(int categoryCode);
    
    /// <summary>
    /// 一括挿入
    /// </summary>
    Task<int> InsertBulkAsync(IEnumerable<T> categories);
    
    /// <summary>
    /// 更新
    /// </summary>
    Task<int> UpdateAsync(T category);
    
    /// <summary>
    /// 削除
    /// </summary>
    Task<int> DeleteAsync(int categoryCode);
    
    /// <summary>
    /// すべて削除（テーブルクリア）
    /// </summary>
    Task<int> DeleteAllAsync();
    
    /// <summary>
    /// 挿入または更新（Upsert）
    /// </summary>
    Task<int> UpsertAsync(T category);
    
    /// <summary>
    /// 一括挿入または更新（Bulk Upsert）
    /// </summary>
    Task<int> UpsertBulkAsync(IEnumerable<T> categories);
}