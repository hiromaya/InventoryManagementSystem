using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 入金伝票リポジトリインターフェース
/// </summary>
public interface IReceiptVoucherRepository
{
    /// <summary>
    /// データセットIDで取得
    /// </summary>
    Task<IEnumerable<ReceiptVoucher>> GetByDataSetIdAsync(string dataSetId);
    
    /// <summary>
    /// ジョブ日付範囲で取得
    /// </summary>
    Task<IEnumerable<ReceiptVoucher>> GetByJobDateRangeAsync(DateTime startDate, DateTime endDate);
    
    /// <summary>
    /// 得意先コードで取得
    /// </summary>
    Task<IEnumerable<ReceiptVoucher>> GetByCustomerCodeAsync(string customerCode);
    
    /// <summary>
    /// 伝票番号で取得
    /// </summary>
    Task<ReceiptVoucher?> GetByVoucherNumberAsync(string voucherNumber);
    
    /// <summary>
    /// 一括挿入
    /// </summary>
    Task<int> InsertBulkAsync(IEnumerable<ReceiptVoucher> vouchers);
    
    /// <summary>
    /// ジョブ日付範囲での削除
    /// </summary>
    Task<int> DeleteByJobDateRangeAsync(DateTime startDate, DateTime endDate);
    
    /// <summary>
    /// データセットIDでの削除
    /// </summary>
    Task<int> DeleteByDataSetIdAsync(string dataSetId);
    
    /// <summary>
    /// 存在確認
    /// </summary>
    Task<bool> ExistsAsync(string voucherNumber, int lineNumber);
    
    /// <summary>
    /// 期間内の合計金額を取得
    /// </summary>
    Task<decimal> GetTotalAmountByPeriodAsync(DateTime startDate, DateTime endDate);
    
    /// <summary>
    /// 得意先別の合計金額を取得
    /// </summary>
    Task<Dictionary<string, decimal>> GetTotalAmountByCustomerAsync(DateTime startDate, DateTime endDate);
}