using InventorySystem.Core.Entities.Masters;

namespace InventorySystem.Core.Interfaces.Masters;

/// <summary>
/// 商品マスタリポジトリインターフェース
/// </summary>
public interface IProductMasterRepository
{
    /// <summary>
    /// 商品コードで取得
    /// </summary>
    Task<ProductMaster?> GetByCodeAsync(string productCode);

    /// <summary>
    /// すべての商品を取得
    /// </summary>
    Task<IEnumerable<ProductMaster>> GetAllAsync();

    /// <summary>
    /// 在庫管理対象商品のみ取得
    /// </summary>
    Task<IEnumerable<ProductMaster>> GetStockManagedAsync();

    /// <summary>
    /// 一括挿入
    /// </summary>
    Task<int> InsertBulkAsync(IEnumerable<ProductMaster> products);

    /// <summary>
    /// 更新
    /// </summary>
    Task<int> UpdateAsync(ProductMaster product);

    /// <summary>
    /// 削除
    /// </summary>
    Task<int> DeleteAsync(string productCode);

    /// <summary>
    /// 存在確認
    /// </summary>
    Task<bool> ExistsAsync(string productCode);

    /// <summary>
    /// 商品名で検索
    /// </summary>
    Task<IEnumerable<ProductMaster>> SearchByNameAsync(string name);

    /// <summary>
    /// 分類コードで商品を取得
    /// </summary>
    Task<IEnumerable<ProductMaster>> GetByCategoryAsync(string categoryType, string categoryCode);

    /// <summary>
    /// 単位コードで商品を取得
    /// </summary>
    Task<IEnumerable<ProductMaster>> GetByUnitCodeAsync(string unitCode);

    /// <summary>
    /// すべて削除（テーブルクリア）
    /// </summary>
    Task<int> DeleteAllAsync();

    /// <summary>
    /// 挿入または更新（Upsert）
    /// </summary>
    Task<int> UpsertAsync(ProductMaster product);

    /// <summary>
    /// 一括挿入または更新（Bulk Upsert）
    /// </summary>
    Task<int> UpsertBulkAsync(IEnumerable<ProductMaster> products);
}