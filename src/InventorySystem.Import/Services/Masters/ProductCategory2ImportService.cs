using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models.Csv.Masters;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 商品分類2マスタCSV取込サービス
/// </summary>
public class ProductCategory2ImportService : MasterImportServiceBase<ProductCategory2Master, ProductCategory2MasterCsv>
{
    protected override string FileNamePattern => "商品分類２";
    public override string ServiceName => "商品分類2マスタ";
    public override int ProcessOrder => 21;

    public ProductCategory2ImportService(
        ICategoryMasterRepository<ProductCategory2Master> repository,
        IUnifiedDataSetService unifiedDataSetService,
        ILogger<ProductCategory2ImportService> logger)
        : base(repository, unifiedDataSetService, logger)
    {
    }
}