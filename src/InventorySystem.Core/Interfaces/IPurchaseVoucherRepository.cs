using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface IPurchaseVoucherRepository
{
    Task<IEnumerable<PurchaseVoucher>> GetByJobDateAsync(DateTime jobDate);
    Task<IEnumerable<PurchaseVoucher>> GetByDataSetIdAsync(string dataSetId);
    Task<string?> GetDataSetIdByJobDateAsync(DateTime jobDate);
    Task<int> CreateAsync(PurchaseVoucher voucher);
    Task<int> BulkInsertAsync(IEnumerable<PurchaseVoucher> vouchers);
    Task<int> DeleteByJobDateAsync(DateTime jobDate);
    Task<int> GetCountAsync(DateTime jobDate);
    Task<int> GetCountByJobDateAsync(DateTime jobDate);
    Task<decimal> GetTotalAmountAsync(DateTime jobDate);
    Task<int> GetModifiedAfterAsync(DateTime jobDate, DateTime modifiedAfter);
    Task<IEnumerable<PurchaseVoucher>> GetAllAsync();
    
    /// <summary>
    /// 指定されたDataSetIdの伝票データのIsActiveフラグを更新
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="isActive">アクティブフラグの値</param>
    /// <returns>更新件数</returns>
    Task<int> UpdateIsActiveByDataSetIdAsync(string dataSetId, bool isActive);
    
    /// <summary>
    /// アクティブな伝票のみを取得（IsActive = true）
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>アクティブな仕入伝票一覧</returns>
    Task<IEnumerable<PurchaseVoucher>> GetActiveByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// 指定されたJobDateとProcessTypeの伝票データを無効化
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <param name="excludeDataSetId">除外するDataSetId（nullの場合は除外しない）</param>
    /// <returns>無効化件数</returns>
    Task<int> DeactivateByJobDateAsync(DateTime jobDate, string? excludeDataSetId = null);
}