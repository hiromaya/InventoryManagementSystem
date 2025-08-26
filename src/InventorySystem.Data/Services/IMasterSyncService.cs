using InventorySystem.Core.Models;
using Microsoft.Data.SqlClient;

namespace InventorySystem.Data.Services;

public interface IMasterSyncService
{
    Task<MasterSyncResult> SyncFromCpInventoryMasterAsync(DateTime jobDate);
    Task UpdateCpInventoryMasterNamesAsync(SqlConnection connection, SqlTransaction transaction, DateTime jobDate);
}

public class MasterSyncResult
{
    public int GradeInserted { get; set; }
    public int GradeSkipped { get; set; }
    public int ClassInserted { get; set; }
    public int ClassSkipped { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}