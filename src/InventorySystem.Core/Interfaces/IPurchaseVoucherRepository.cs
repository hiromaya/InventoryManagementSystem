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
}