using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models.Csv.Masters;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 商品分類3マスタCSV取込サービス
/// </summary>
public class ProductCategory3ImportService : MasterImportServiceBase<ProductCategory3Master, ProductCategory3MasterCsv>
{
    protected override string FileNamePattern => "商品分類３";
    public override string ServiceName => "商品分類3マスタ";
    public override int ProcessOrder => 22;

    public ProductCategory3ImportService(
        ICategoryMasterRepository<ProductCategory3Master> repository,
        IDataSetService unifiedDataSetService,
        ILogger<ProductCategory3ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}