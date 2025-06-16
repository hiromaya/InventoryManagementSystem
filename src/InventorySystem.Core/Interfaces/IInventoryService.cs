using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface IInventoryService
{
    Task<string> ProcessDailyInventoryAsync(DateTime jobDate);
    Task<decimal> CalculateGrossProfitAsync(DateTime jobDate);
    Task<int> ClearDailyAreaAsync(DateTime jobDate);
    Task<bool> RollbackProcessingAsync(string dataSetId);
}