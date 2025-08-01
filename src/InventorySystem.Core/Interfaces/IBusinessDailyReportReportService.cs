using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces
{
    public interface IBusinessDailyReportReportService
    {
        byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate);
    }
}