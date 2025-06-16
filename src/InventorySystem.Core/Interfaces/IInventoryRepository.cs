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
}