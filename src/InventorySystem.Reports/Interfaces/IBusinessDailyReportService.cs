using InventorySystem.Core.Entities;

namespace InventorySystem.Reports.Interfaces
{
    public interface IBusinessDailyReportService
    {
        byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate);
    }
}