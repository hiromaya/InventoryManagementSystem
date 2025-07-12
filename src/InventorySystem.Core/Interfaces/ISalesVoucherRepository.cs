using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface ISalesVoucherRepository
{
    Task<IEnumerable<SalesVoucher>> GetByJobDateAsync(DateTime jobDate);
    Task<int> CreateAsync(SalesVoucher voucher);
    Task<int> BulkInsertAsync(IEnumerable<SalesVoucher> vouchers);
    Task<int> DeleteByJobDateAsync(DateTime jobDate);
    Task<int> GetCountAsync(DateTime jobDate);
    Task<int> GetCountByJobDateAsync(DateTime jobDate);
    Task<decimal> GetTotalAmountAsync(DateTime jobDate);
    Task<int> GetModifiedAfterAsync(DateTime jobDate, DateTime modifiedAfter);
    Task<IEnumerable<SalesVoucher>> GetAllAsync();
}