using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models.Csv.Masters;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 得意先分類1マスタCSV取込サービス
/// </summary>
public class CustomerCategory1ImportService : MasterImportServiceBase<CustomerCategory1Master, CustomerCategory1MasterCsv>
{
    protected override string FileNamePattern => "得意先分類１";
    public override string ServiceName => "得意先分類1マスタ";
    public override int ProcessOrder => 30;

    public CustomerCategory1ImportService(
        ICategoryMasterRepository<CustomerCategory1Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<CustomerCategory1ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}

/// <summary>
/// 得意先分類2マスタCSV取込サービス
/// </summary>
public class CustomerCategory2ImportService : MasterImportServiceBase<CustomerCategory2Master, CustomerCategory2MasterCsv>
{
    protected override string FileNamePattern => "得意先分類２";
    public override string ServiceName => "得意先分類2マスタ";
    public override int ProcessOrder => 31;

    public CustomerCategory2ImportService(
        ICategoryMasterRepository<CustomerCategory2Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<CustomerCategory2ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}

/// <summary>
/// 得意先分類3マスタCSV取込サービス
/// </summary>
public class CustomerCategory3ImportService : MasterImportServiceBase<CustomerCategory3Master, CustomerCategory3MasterCsv>
{
    protected override string FileNamePattern => "得意先分類３";
    public override string ServiceName => "得意先分類3マスタ";
    public override int ProcessOrder => 32;

    public CustomerCategory3ImportService(
        ICategoryMasterRepository<CustomerCategory3Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<CustomerCategory3ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}

/// <summary>
/// 得意先分類4マスタCSV取込サービス
/// </summary>
public class CustomerCategory4ImportService : MasterImportServiceBase<CustomerCategory4Master, CustomerCategory4MasterCsv>
{
    protected override string FileNamePattern => "得意先分類４";
    public override string ServiceName => "得意先分類4マスタ";
    public override int ProcessOrder => 33;

    public CustomerCategory4ImportService(
        ICategoryMasterRepository<CustomerCategory4Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<CustomerCategory4ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}

/// <summary>
/// 得意先分類5マスタCSV取込サービス
/// </summary>
public class CustomerCategory5ImportService : MasterImportServiceBase<CustomerCategory5Master, CustomerCategory5MasterCsv>
{
    protected override string FileNamePattern => "得意先分類５";
    public override string ServiceName => "得意先分類5マスタ";
    public override int ProcessOrder => 34;

    public CustomerCategory5ImportService(
        ICategoryMasterRepository<CustomerCategory5Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<CustomerCategory5ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}