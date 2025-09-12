using InventorySystem.Core.Entities.Masters;

namespace InventorySystem.Core.Interfaces.Masters;

/// <summary>
/// 得意先マスタリポジトリインターフェース
/// </summary>
public interface ICustomerMasterRepository
{
    /// <summary>
    /// 得意先コードで取得
    /// </summary>
    Task<CustomerMaster?> GetByCodeAsync(string customerCode);

    /// <summary>
    /// すべての得意先を取得
    /// </summary>
    Task<IEnumerable<CustomerMaster>> GetAllAsync();

    /// <summary>
    /// アクティブな得意先のみ取得
    /// </summary>
    Task<IEnumerable<CustomerMaster>> GetActiveAsync();

    /// <summary>
    /// 一括挿入
    /// </summary>
    Task<int> InsertBulkAsync(IEnumerable<CustomerMaster> customers);

    /// <summary>
    /// 更新
    /// </summary>
    Task<int> UpdateAsync(CustomerMaster customer);

    /// <summary>
    /// 削除（論理削除）
    /// </summary>
    Task<int> DeleteAsync(string customerCode);

    /// <summary>
    /// 存在確認
    /// </summary>
    Task<bool> ExistsAsync(string customerCode);

    /// <summary>
    /// 得意先名で検索
    /// </summary>
    Task<IEnumerable<CustomerMaster>> SearchByNameAsync(string name);

    /// <summary>
    /// 請求先コードで得意先を取得
    /// </summary>
    Task<IEnumerable<CustomerMaster>> GetByBillingCodeAsync(string billingCode);

    /// <summary>
    /// 分類コードで得意先を取得
    /// </summary>
    Task<IEnumerable<CustomerMaster>> GetByCategoryAsync(string categoryType, string categoryCode);

    /// <summary>
    /// すべて削除（テーブルクリア）
    /// </summary>
    Task<int> DeleteAllAsync();

    /// <summary>
    /// 挿入または更新（Upsert）
    /// </summary>
    Task<int> UpsertAsync(CustomerMaster customer);

    /// <summary>
    /// 一括挿入または更新（Bulk Upsert）
    /// </summary>
    Task<int> UpsertBulkAsync(IEnumerable<CustomerMaster> customers);
}