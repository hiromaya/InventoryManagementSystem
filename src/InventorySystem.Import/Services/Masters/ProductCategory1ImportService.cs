using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models.Csv.Masters;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 商品分類1マスタCSV取込サービス
/// </summary>
public class ProductCategory1ImportService : MasterImportServiceBase<ProductCategory1Master, ProductCategory1MasterCsv>
{
    protected override string FileNamePattern => "商品分類１";
    public override string ServiceName => "商品分類1マスタ";
    public override int ProcessOrder => 20;

    public ProductCategory1ImportService(
        ICategoryMasterRepository<ProductCategory1Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<ProductCategory1ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}