using System;

namespace InventorySystem.Reports.Interfaces
{
    public interface IInventoryListReportService
    {
        byte[] GenerateInventoryListReport(DateTime jobDate, string? departmentCode = null);
    }
}

