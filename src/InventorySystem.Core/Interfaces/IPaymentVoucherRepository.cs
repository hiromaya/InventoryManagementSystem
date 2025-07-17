using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 支払伝票リポジトリインターフェース
/// </summary>
public interface IPaymentVoucherRepository
{
    /// <summary>
    /// データセットIDで取得
    /// </summary>
    Task<IEnumerable<PaymentVoucher>> GetByDataSetIdAsync(string dataSetId);
    
    /// <summary>
    /// ジョブ日付範囲で取得
    /// </summary>
    Task<IEnumerable<PaymentVoucher>> GetByJobDateRangeAsync(DateTime startDate, DateTime endDate);
    
    /// <summary>
    /// 仕入先コードで取得
    /// </summary>
    Task<IEnumerable<PaymentVoucher>> GetBySupplierCodeAsync(string supplierCode);
    
    /// <summary>
    /// 伝票番号で取得
    /// </summary>
    Task<PaymentVoucher?> GetByVoucherNumberAsync(string voucherNumber);
    
    /// <summary>
    /// 一括挿入
    /// </summary>
    Task<int> InsertBulkAsync(IEnumerable<PaymentVoucher> vouchers);
    
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
    /// 仕入先別の合計金額を取得
    /// </summary>
    Task<Dictionary<string, decimal>> GetTotalAmountBySupplierAsync(DateTime startDate, DateTime endDate);
}