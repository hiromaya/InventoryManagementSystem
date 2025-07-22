using InventorySystem.Core.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Factories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventorySystem.Import.Services
{
    /// <summary>
    /// DataSetManagementテーブルのみを使用するサービス
    /// </summary>
    public class DataSetManagementService : IDataSetService
    {
        private readonly IDataSetManagementRepository _repository;
        private readonly ILogger<DataSetManagementService> _logger;
        private readonly FeatureFlags _features;
        private readonly ITimeProvider _timeProvider;
        private readonly IDataSetManagementFactory _dataSetFactory;
        
        public DataSetManagementService(
            IDataSetManagementRepository repository,
            ILogger<DataSetManagementService> logger,
            IOptions<FeatureFlags> features,
            ITimeProvider timeProvider,
            IDataSetManagementFactory dataSetFactory)
        {
            _repository = repository;
            _logger = logger;
            _features = features.Value;
            _timeProvider = timeProvider;
            _dataSetFactory = dataSetFactory;
        }
        
        public async Task<string> CreateDataSetAsync(
            string name,
            string processType,
            DateTime jobDate,
            string? description = null,
            string? filePath = null)
        {
            var dataSetId = Guid.NewGuid().ToString();
            
            // ⭐ ファクトリパターンでJST統一時刻で作成（Gemini推奨）
            var dataSetManagement = _dataSetFactory.CreateNew(
                dataSetId,
                jobDate,
                processType,
                "system",
                "DeptA",
                "IMPORT",
                null, // importedFiles
                BuildNotes(name, description)
            );
            
            // ユースケース固有のプロパティ追加設定
            dataSetManagement.Name = name;
            dataSetManagement.Description = description;
            dataSetManagement.FilePath = filePath;
            dataSetManagement.Status = "Processing";
            
            await _repository.CreateAsync(dataSetManagement);
            
            // ログ出力（FeatureFlag削除後は常に有効）
            _logger.LogInformation(
                "DataSetManagement作成: Id={DataSetId}, ProcessType={ProcessType}, JobDate={JobDate}",
                dataSetId, processType, jobDate);
            
            return dataSetId;
        }
        
        public async Task UpdateStatusAsync(string dataSetId, string status)
        {
            var dataSet = await _repository.GetByIdAsync(dataSetId);
            if (dataSet == null)
            {
                _logger.LogWarning("DataSetManagement not found: {DataSetId}", dataSetId);
                return;
            }
            
            // ステータスの更新（JST統一）
            dataSet.Status = status;
            dataSet.UpdatedAt = _timeProvider.Now.DateTime;
            
            // ステータスに応じてフラグも更新
            switch (status)
            {
                case "Completed":
                case "Imported":
                    dataSet.IsActive = true;
                    dataSet.IsArchived = false;
                    break;
                case "Error":
                case "Failed":
                    dataSet.IsActive = false;
                    dataSet.IsArchived = true;
                    dataSet.ArchivedAt = _timeProvider.Now.DateTime;
                    dataSet.ArchivedBy = "system";
                    break;
            }
            
            await _repository.UpdateAsync(dataSet);
            
            // ログ出力（FeatureFlag削除後は常に有効）
            _logger.LogInformation(
                "DataSetManagementステータス更新: Id={DataSetId}, Status={Status}",
                dataSetId, status);
        }
        
        public async Task UpdateRecordCountAsync(string dataSetId, int recordCount)
        {
            var dataSet = await _repository.GetByIdAsync(dataSetId);
            if (dataSet == null) return;
            
            dataSet.RecordCount = recordCount;
            dataSet.TotalRecordCount = recordCount;
            dataSet.UpdatedAt = _timeProvider.Now.DateTime;
            await _repository.UpdateAsync(dataSet);
        }
        
        public async Task SetErrorAsync(string dataSetId, string errorMessage)
        {
            var dataSet = await _repository.GetByIdAsync(dataSetId);
            if (dataSet == null) return;
            
            dataSet.ErrorMessage = errorMessage;
            dataSet.Status = "Error";
            dataSet.IsActive = false;
            dataSet.IsArchived = true;
            dataSet.ArchivedAt = _timeProvider.Now.DateTime;
            dataSet.ArchivedBy = "system";
            dataSet.UpdatedAt = _timeProvider.Now.DateTime;
            
            await _repository.UpdateAsync(dataSet);
            
            // ログ出力（FeatureFlag削除後は常に有効）
            _logger.LogInformation(
                "DataSetManagementエラー設定: Id={DataSetId}, Error={Error}",
                dataSetId, errorMessage);
        }
        
        public async Task<bool> ExistsAsync(string dataSetId)
        {
            return await _repository.GetByIdAsync(dataSetId) != null;
        }
        
        public async Task<DataSetInfo?> GetByIdAsync(string dataSetId)
        {
            var dataSet = await _repository.GetByIdAsync(dataSetId);
            if (dataSet == null) return null;
            
            return new DataSetInfo
            {
                Id = dataSet.DataSetId,
                Name = dataSet.Name ?? ExtractNameFromNotes(dataSet.Notes),
                ProcessType = dataSet.ProcessType,
                Status = dataSet.Status ?? (dataSet.IsActive ? "Completed" : "Error"),
                JobDate = dataSet.JobDate,
                RecordCount = dataSet.RecordCount,
                ErrorMessage = dataSet.ErrorMessage,
                CreatedAt = dataSet.CreatedAt,
                UpdatedAt = dataSet.UpdatedAt,
                IsActive = dataSet.IsActive,
                FilePath = dataSet.FilePath,
                Description = dataSet.Description ?? ExtractDescriptionFromNotes(dataSet.Notes)
            };
        }
        
        public async Task UpdateTimestampAsync(string dataSetId)
        {
            var dataSet = await _repository.GetByIdAsync(dataSetId);
            if (dataSet == null) return;
            
            dataSet.UpdatedAt = _timeProvider.Now.DateTime;
            await _repository.UpdateAsync(dataSet);
        }
        
        private static string BuildNotes(string name, string? description)
        {
            if (!string.IsNullOrEmpty(description))
            {
                return $"Name: {name}\nDescription: {description}";
            }
            return $"Name: {name}";
        }
        
        private static string ExtractNameFromNotes(string? notes)
        {
            if (string.IsNullOrEmpty(notes)) return string.Empty;
            
            var lines = notes.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("Name: "))
                {
                    return line.Substring(6);
                }
            }
            
            return string.Empty;
        }
        
        private static string ExtractDescriptionFromNotes(string? notes)
        {
            if (string.IsNullOrEmpty(notes)) return string.Empty;
            
            var lines = notes.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("Description: "))
                {
                    return line.Substring(13);
                }
            }
            
            return string.Empty;
        }
    }
}