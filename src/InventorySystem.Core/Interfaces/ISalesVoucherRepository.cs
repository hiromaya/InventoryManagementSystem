using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface ISalesVoucherRepository
{
    Task<IEnumerable<SalesVoucher>> GetByJobDateAsync(DateTime jobDate);
    Task<IEnumerable<SalesVoucher>> GetByDataSetIdAsync(string dataSetId);
    Task<string?> GetDataSetIdByJobDateAsync(DateTime jobDate);
    Task<int> CreateAsync(SalesVoucher voucher);
    Task<int> BulkInsertAsync(IEnumerable<SalesVoucher> vouchers);
    Task<int> DeleteByJobDateAsync(DateTime jobDate);
    Task<int> GetCountAsync(DateTime jobDate);
    Task<int> GetCountByJobDateAsync(DateTime jobDate);
    Task<decimal> GetTotalAmountAsync(DateTime jobDate);
    Task<int> GetModifiedAfterAsync(DateTime jobDate, DateTime modifiedAfter);
    Task<IEnumerable<SalesVoucher>> GetAllAsync();
    
    /// <summary>
    /// Process 2-5: JobDateとDataSetIdで売上伝票を取得
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <param name="dataSetId">データセットID</param>
    /// <returns>売上伝票一覧</returns>
    Task<IEnumerable<SalesVoucher>> GetByJobDateAndDataSetIdAsync(DateTime jobDate, string dataSetId);
    
    /// <summary>
    /// Process 2-5: 売上伝票の在庫単価と粗利益をバッチ更新
    /// </summary>
    /// <param name="vouchers">更新対象の売上伝票</param>
    /// <returns>更新件数</returns>
    Task<int> UpdateInventoryUnitPriceAndGrossProfitBatchAsync(IEnumerable<SalesVoucher> vouchers);
    
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
    /// <returns>アクティブな売上伝票一覧</returns>
    Task<IEnumerable<SalesVoucher>> GetActiveByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// 指定されたJobDateとProcessTypeの伝票データを無効化
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <param name="excludeDataSetId">除外するDataSetId（nullの場合は除外しない）</param>
    /// <returns>無効化件数</returns>
    Task<int> DeactivateByJobDateAsync(DateTime jobDate, string? excludeDataSetId = null);
}