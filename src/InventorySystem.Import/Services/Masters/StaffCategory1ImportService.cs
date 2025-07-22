using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models.Csv.Masters;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 担当者分類1マスタCSV取込サービス
/// </summary>
public class StaffCategory1ImportService : MasterImportServiceBase<StaffCategory1Master, StaffCategory1MasterCsv>
{
    protected override string FileNamePattern => "担当者分類１";
    public override string ServiceName => "担当者分類1マスタ";
    public override int ProcessOrder => 13;

    public StaffCategory1ImportService(
        ICategoryMasterRepository<StaffCategory1Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<StaffCategory1ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}