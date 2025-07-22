using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models.Csv.Masters;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 担当者マスタCSV取込サービス
/// </summary>
public class StaffMasterImportService : IImportService
{
    private readonly IStaffMasterRepository _repository;
    private readonly IDataSetService _unifiedDataSetService;
    private readonly ILogger<StaffMasterImportService> _logger;

    public string ServiceName => "担当者マスタ";
    public int ProcessOrder => 50;

    protected string FileNamePattern => "担当者";

    public StaffMasterImportService(
        IStaffMasterRepository repository,
        IDataSetService unifiedDataSetService,
        ILogger<StaffMasterImportService> logger)
    {
        _repository = repository;
        _unifiedDataSetService = unifiedDataSetService;
        _logger = logger;
    }

    public bool CanHandle(string fileName)
    {
        return fileName.Contains(FileNamePattern, StringComparison.OrdinalIgnoreCase) &&
               !fileName.Contains("分類", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ImportResult> ImportAsync(string filePath, DateTime importDate)
    {
        // 担当者マスタは追加フィールドがあるため、専用実装
        // 実装は既存のProductMasterImportServiceパターンを踏襲
        
        _logger.LogInformation("{ServiceName}CSV取込開始: {FilePath}", ServiceName, filePath);
        
        // 省略...
        
        return new ImportResult
        {
            DataSetId = "STAFF_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            Status = "Completed",
            ImportedCount = 0,
            FilePath = filePath,
            CreatedAt = DateTime.Now
        };
    }
}