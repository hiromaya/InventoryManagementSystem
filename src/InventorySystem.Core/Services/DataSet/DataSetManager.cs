using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Factories;
using System.Text.Json;

namespace InventorySystem.Core.Services.DataSet;

/// <summary>
/// データセット管理サービス実装
/// </summary>
public class DataSetManager : IDataSetManager
{
    private readonly IDataSetManagementRepository _repository;
    private readonly IDataSetManagementFactory _factory;
    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<DataSetManager> _logger;
    
    public DataSetManager(
        IDataSetManagementRepository repository,
        IDataSetManagementFactory factory,
        ITimeProvider timeProvider,
        ILogger<DataSetManager> logger)
    {
        _repository = repository;
        _factory = factory;
        _timeProvider = timeProvider;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public string GenerateDataSetId(DateTime jobDate, string processType)
    {
        // フォーマット: DS_{yyyyMMdd}_{HHmmss}_{ProcessType}
        // ⭐ Phase 2-B: ITimeProvider使用（Gemini推奨）
        var dataSetId = $"DS_{jobDate:yyyyMMdd}_{_timeProvider.Now:HHmmss}_{processType}";
        
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
    /// インポートファイル情報を作成（ファクトリパターン移行）
    /// ⭐ Phase 2-B: 静的メソッドからインスタンスメソッドに変更（Gemini推奨）
    /// </summary>
    public DataSetManagement CreateDataSet(
        string dataSetId,
        DateTime jobDate,
        string processType,
        List<string>? importedFiles = null,
        string createdBy = "System",
        string department = "DeptA")
    {
        // ⭐ Phase 2-B: ファクトリ経由でエンティティ作成（Gemini推奨）
        return _factory.CreateNew(
            dataSetId,
            jobDate,
            processType,
            createdBy,
            department,
            importType: null,  // processTypeから自動判定
            importedFiles,
            notes: null);
    }
}