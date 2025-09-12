using System;
using System.Collections.Generic;
using InventorySystem.Core.Entities;

namespace InventorySystem.Reports.Interfaces
{
    public interface IUnmatchListReportService
    {
        byte[] GenerateUnmatchListReport(IEnumerable<UnmatchItem> unmatchItems, DateTime jobDate);
    }

    public interface IDailyReportService
    {
        byte[] GenerateDailyReport(
            List<DailyReportItem> items, 
            List<DailyReportSubtotal> subtotals, 
            DailyReportTotal total, 
            DateTime reportDate);
    }
}