using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using System.Text.Json;

namespace InventorySystem.Core.Services.Dataset;

/// <summary>
/// データセット管理サービス実装
/// </summary>
public class DatasetManager : IDatasetManager
{
    private readonly IDatasetManagementRepository _repository;
    private readonly ILogger<DatasetManager> _logger;
    
    public DatasetManager(
        IDatasetManagementRepository repository,
        ILogger<DatasetManager> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public string GenerateDatasetId(DateTime jobDate, string processType)
    {
        // フォーマット: DS_{yyyyMMdd}_{HHmmss}_{ProcessType}
        var datasetId = $"DS_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}_{processType}";
        
        _logger.LogInformation("データセットID生成: {DatasetId}", datasetId);
        return datasetId;
    }
    
    /// <inheritdoc/>
    public async Task<DatasetManagement> RegisterDataset(DatasetManagement dataset)
    {
        _logger.LogInformation("データセット登録開始: DatasetId={DatasetId}, ProcessType={ProcessType}, JobDate={JobDate}", 
            dataset.DatasetId, dataset.ProcessType, dataset.JobDate);
        
        try
        {
            var result = await _repository.CreateAsync(dataset);
            _logger.LogInformation("データセット登録完了: {DatasetId}", dataset.DatasetId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット登録エラー: {DatasetId}", dataset.DatasetId);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<string> GetLatestDatasetId(string processType, DateTime jobDate)
    {
        var dataset = await _repository.GetLatestByJobDateAndTypeAsync(jobDate, processType);
        return dataset?.DatasetId ?? string.Empty;
    }
    
    /// <inheritdoc/>
    public async Task<DatasetManagement?> GetDataset(string datasetId)
    {
        return await _repository.GetByIdAsync(datasetId);
    }
    
    /// <summary>
    /// インポートファイル情報を作成
    /// </summary>
    public static DatasetManagement CreateDataset(
        string datasetId,
        DateTime jobDate,
        string processType,
        List<string>? importedFiles = null,
        string createdBy = "System")
    {
        return new DatasetManagement
        {
            DatasetId = datasetId,
            JobDate = jobDate,
            ProcessType = processType,
            ImportType = processType switch 
            {
                "IMPORT" => "IMPORT",
                "CARRYOVER" => "CARRYOVER",
                "INIT" => "INIT",
                "MANUAL" => "MANUAL",
                _ => "UNKNOWN"
            },
            RecordCount = 0,  // 呼び出し元で設定
            TotalRecordCount = 0,  // 呼び出し元で設定
            IsActive = true,
            IsArchived = false,
            ParentDataSetId = null,
            ImportedFiles = importedFiles != null ? JsonSerializer.Serialize(importedFiles) : null,
            CreatedAt = DateTime.Now,
            CreatedBy = createdBy,
            Department = "DeptA",  // 呼び出し元で適切に設定
            Notes = null
        };
    }
}