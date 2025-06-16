using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface ISalesVoucherRepository
{
    Task<IEnumerable<SalesVoucher>> GetByJobDateAsync(DateTime jobDate);
    Task<int> CreateAsync(SalesVoucher voucher);
    Task<int> BulkInsertAsync(IEnumerable<SalesVoucher> vouchers);
}