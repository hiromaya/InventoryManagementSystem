using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using System.Text.Json;

namespace InventorySystem.Core.Services.DataSet;

/// <summary>
/// データセット管理サービス実装
/// </summary>
public class DataSetManager : IDataSetManager
{
    private readonly IDataSetManagementRepository _repository;
    private readonly ILogger<DataSetManager> _logger;
    
    public DataSetManager(
        IDataSetManagementRepository repository,
        ILogger<DataSetManager> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public string GenerateDataSetId(DateTime jobDate, string processType)
    {
        // フォーマット: DS_{yyyyMMdd}_{HHmmss}_{ProcessType}
        var dataSetId = $"DS_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}_{processType}";
        
        _logger.LogInformation("データセットID生成: {DataSetId}", dataSetId);
        return dataSetId;
    }
    
    /// <inheritdoc/>
    public async Task<DataSetManagement> RegisterDataSet(DataSetManagement dataSet)
    {
        _logger.LogInformation("データセット登録開始: DataSetId={DataSetId}, ProcessType={ProcessType}, JobDate={JobDate}", 
            dataSet.DataSetId, dataSet.ProcessType, dataSet.JobDate);
        
        try
        {
            var result = await _repository.CreateAsync(dataSet);
            _logger.LogInformation("データセット登録完了: {DataSetId}", dataSet.DataSetId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット登録エラー: {DataSetId}", dataSet.DataSetId);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<string> GetLatestDataSetId(string processType, DateTime jobDate)
    {
        var dataSet = await _repository.GetLatestByJobDateAndTypeAsync(jobDate, processType);
        return dataSet?.DataSetId ?? string.Empty;
    }
    
    /// <inheritdoc/>
    public async Task<DataSetManagement?> GetDataSet(string dataSetId)
    {
        return await _repository.GetByIdAsync(dataSetId);
    }
    
    /// <summary>
    /// インポートファイル情報を作成
    /// </summary>
    public static DataSetManagement CreateDataSet(
        string dataSetId,
        DateTime jobDate,
        string processType,
        List<string>? importedFiles = null,
        string createdBy = "System")
    {
        return new DataSetManagement
        {
            DataSetId = dataSetId,
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