using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models.Csv.Masters;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 仕入先分類1マスタCSV取込サービス
/// </summary>
public class SupplierCategory1ImportService : MasterImportServiceBase<SupplierCategory1Master, SupplierCategory1MasterCsv>
{
    protected override string FileNamePattern => "仕入先分類１";
    public override string ServiceName => "仕入先分類1マスタ";
    public override int ProcessOrder => 40;

    public SupplierCategory1ImportService(
        ICategoryMasterRepository<SupplierCategory1Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<SupplierCategory1ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}

/// <summary>
/// 仕入先分類2マスタCSV取込サービス
/// </summary>
public class SupplierCategory2ImportService : MasterImportServiceBase<SupplierCategory2Master, SupplierCategory2MasterCsv>
{
    protected override string FileNamePattern => "仕入先分類２";
    public override string ServiceName => "仕入先分類2マスタ";
    public override int ProcessOrder => 41;

    public SupplierCategory2ImportService(
        ICategoryMasterRepository<SupplierCategory2Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<SupplierCategory2ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}

/// <summary>
/// 仕入先分類3マスタCSV取込サービス
/// </summary>
public class SupplierCategory3ImportService : MasterImportServiceBase<SupplierCategory3Master, SupplierCategory3MasterCsv>
{
    protected override string FileNamePattern => "仕入先分類３";
    public override string ServiceName => "仕入先分類3マスタ";
    public override int ProcessOrder => 42;

    public SupplierCategory3ImportService(
        ICategoryMasterRepository<SupplierCategory3Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<SupplierCategory3ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}