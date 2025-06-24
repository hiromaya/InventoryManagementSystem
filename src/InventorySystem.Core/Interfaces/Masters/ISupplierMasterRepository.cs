using InventorySystem.Core.Entities.Masters;

namespace InventorySystem.Core.Interfaces.Masters;

/// <summary>
/// 仕入先マスタリポジトリインターフェース
/// </summary>
public interface ISupplierMasterRepository
{
    /// <summary>
    /// 仕入先コードで取得
    /// </summary>
    Task<SupplierMaster?> GetByCodeAsync(string supplierCode);

    /// <summary>
    /// すべての仕入先を取得
    /// </summary>
    Task<IEnumerable<SupplierMaster>> GetAllAsync();

    /// <summary>
    /// アクティブな仕入先のみ取得
    /// </summary>
    Task<IEnumerable<SupplierMaster>> GetActiveAsync();

    /// <summary>
    /// 一括挿入
    /// </summary>
    Task<int> InsertBulkAsync(IEnumerable<SupplierMaster> suppliers);

    /// <summary>
    /// 更新
    /// </summary>
    Task<int> UpdateAsync(SupplierMaster supplier);

    /// <summary>
    /// 削除（論理削除）
    /// </summary>
    Task<int> DeleteAsync(string supplierCode);

    /// <summary>
    /// 存在確認
    /// </summary>
    Task<bool> ExistsAsync(string supplierCode);

    /// <summary>
    /// 仕入先名で検索
    /// </summary>
    Task<IEnumerable<SupplierMaster>> SearchByNameAsync(string name);

    /// <summary>
    /// 支払先コードで仕入先を取得
    /// </summary>
    Task<IEnumerable<SupplierMaster>> GetByPaymentCodeAsync(string paymentCode);

    /// <summary>
    /// 奨励金対象の仕入先を取得
    /// </summary>
    Task<IEnumerable<SupplierMaster>> GetIncentiveTargetsAsync();

    /// <summary>
    /// すべて削除（テーブルクリア）
    /// </summary>
    Task<int> DeleteAllAsync();

    /// <summary>
    /// 挿入または更新（Upsert）
    /// </summary>
    Task<int> UpsertAsync(SupplierMaster supplier);

    /// <summary>
    /// 一括挿入または更新（Bulk Upsert）
    /// </summary>
    Task<int> UpsertBulkAsync(IEnumerable<SupplierMaster> suppliers);
}