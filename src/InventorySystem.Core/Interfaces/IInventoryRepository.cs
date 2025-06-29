using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface IInventoryRepository
{
    Task<IEnumerable<InventoryMaster>> GetByJobDateAsync(DateTime jobDate);
    Task<InventoryMaster?> GetByKeyAsync(InventoryKey key, DateTime jobDate);
    Task<int> CreateAsync(InventoryMaster inventory);
    Task<int> UpdateAsync(InventoryMaster inventory);
    Task<int> DeleteByJobDateAsync(DateTime jobDate);
    Task<int> ClearDailyFlagAsync(DateTime jobDate);
    Task<int> BulkInsertAsync(IEnumerable<InventoryMaster> inventories);
    
    /// <summary>
    /// 売上・仕入伝票に対応する在庫マスタのJobDateを更新する
    /// </summary>
    Task<int> UpdateJobDateForVouchersAsync(DateTime jobDate);
    
    /// <summary>
    /// 新規商品を在庫マスタに登録する
    /// </summary>
    Task<int> RegisterNewProductsAsync(DateTime jobDate);
    
    /// <summary>
    /// CP在庫マスタから在庫マスタを更新する（日次終了処理用）
    /// </summary>
    Task<int> UpdateFromCpInventoryAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// 在庫マスタから任意の日付で商品キーに一致するレコードを取得（最新日付優先）
    /// </summary>
    Task<InventoryMaster?> GetByKeyAnyDateAsync(InventoryKey key);
    
    /// <summary>
    /// 売上・仕入・在庫調整から在庫マスタの初期データを作成
    /// </summary>
    Task<int> CreateInitialInventoryFromVouchersAsync(DateTime jobDate);
}