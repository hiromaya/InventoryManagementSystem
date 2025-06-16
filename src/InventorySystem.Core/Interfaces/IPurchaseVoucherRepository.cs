using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface IPurchaseVoucherRepository
{
    Task<IEnumerable<PurchaseVoucher>> GetByJobDateAsync(DateTime jobDate);
    Task<int> CreateAsync(PurchaseVoucher voucher);
    Task<int> BulkInsertAsync(IEnumerable<PurchaseVoucher> vouchers);
}