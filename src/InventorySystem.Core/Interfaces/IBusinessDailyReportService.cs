using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces
{
    public interface IBusinessDailyReportService
    {
        Task<BusinessDailyReportResult> ExecuteAsync(DateTime jobDate, string dataSetId);
    }
}