using InventorySystem.Core.Entities;
using InventorySystem.Reports.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Reports.Services
{
    public class PlaceholderUnmatchListReportService : IUnmatchListReportService
    {
        public byte[] GenerateUnmatchListReport(IEnumerable<UnmatchItem> unmatchItems, DateTime jobDate)
        {
            var message = "PDF generation is only available on Windows environment.";
            return Encoding.UTF8.GetBytes(message);
        }
    }
    
    public class PlaceholderDailyReportService : IDailyReportService
    {
        public byte[] GenerateDailyReport(
            List<DailyReportItem> items, 
            List<DailyReportSubtotal> subtotals, 
            DailyReportTotal total, 
            DateTime reportDate)
        {
            var message = "PDF generation is only available on Windows environment.";
            return Encoding.UTF8.GetBytes(message);
        }
    }
}