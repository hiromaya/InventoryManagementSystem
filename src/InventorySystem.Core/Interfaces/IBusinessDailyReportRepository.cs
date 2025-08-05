using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces
{
    public interface IBusinessDailyReportRepository
    {
        Task ClearDailyAreaAsync();
        Task AggregateSalesDataAsync(DateTime jobDate);
        Task AggregatePurchaseDataAsync(DateTime jobDate);
        Task AggregateReceiptDataAsync(DateTime jobDate);
        Task AggregatePaymentDataAsync(DateTime jobDate);
        Task<List<BusinessDailyReportItem>> GetReportDataAsync();
        Task<List<BusinessDailyReportItem>> GetMonthlyDataAsync(DateTime jobDate);
        Task<List<BusinessDailyReportItem>> GetYearlyDataAsync(DateTime jobDate);
        Task UpdateClassificationNamesAsync();
    }
}