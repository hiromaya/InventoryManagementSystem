using InventorySystem.Core.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
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
        
        public DataSetManagementService(
            IDataSetManagementRepository repository,
            ILogger<DataSetManagementService> logger,
            IOptions<FeatureFlags> features)
        {
            _repository = repository;
            _logger = logger;
            _features = features.Value;
        }
        
        public async Task<string> CreateDataSetAsync(
            string name,
            string processType,
            DateTime jobDate,
            string? description = null,
            string? filePath = null)
        {
            var dataSetId = Guid.NewGuid().ToString();
            
            var dataSetManagement = new DataSetManagement
            {
                DataSetId = dataSetId,
                JobDate = jobDate,
                ProcessType = processType,
                ImportType = "IMPORT",
                RecordCount = 0,
                TotalRecordCount = 0,
                IsActive = true,
                IsArchived = false,
                CreatedAt = DateTime.Now,
                CreatedBy = "system",
                Department = "DeptA",
                Notes = BuildNotes(name, description),
                // 拡張フィールド（マイグレーションで追加）
                Name = name,
                Description = description,
                FilePath = filePath,
                Status = "Processing",
                UpdatedAt = DateTime.Now
            };
            
            await _repository.CreateAsync(dataSetManagement);
            
            if (_features.EnableDataSetsMigrationLog)
            {
                _logger.LogInformation(
                    "DataSetManagement作成: Id={DataSetId}, ProcessType={ProcessType}, JobDate={JobDate}",
                    dataSetId, processType, jobDate);
            }
            
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
            
            // ステータスの更新
            dataSet.Status = status;
            dataSet.UpdatedAt = DateTime.Now;
            
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
                    dataSet.ArchivedAt = DateTime.Now;
                    dataSet.ArchivedBy = "system";
                    break;
            }
            
            await _repository.UpdateAsync(dataSet);
            
            if (_features.EnableDataSetsMigrationLog)
            {
                _logger.LogInformation(
                    "DataSetManagementステータス更新: Id={DataSetId}, Status={Status}",
                    dataSetId, status);
            }
        }
        
        public async Task UpdateRecordCountAsync(string dataSetId, int recordCount)
        {
            var dataSet = await _repository.GetByIdAsync(dataSetId);
            if (dataSet == null) return;
            
            dataSet.RecordCount = recordCount;
            dataSet.TotalRecordCount = recordCount;
            dataSet.UpdatedAt = DateTime.Now;
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
            dataSet.ArchivedAt = DateTime.Now;
            dataSet.ArchivedBy = "system";
            dataSet.UpdatedAt = DateTime.Now;
            
            await _repository.UpdateAsync(dataSet);
            
            if (_features.EnableDataSetsMigrationLog)
            {
                _logger.LogInformation(
                    "DataSetManagementエラー設定: Id={DataSetId}, Error={Error}",
                    dataSetId, errorMessage);
            }
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
            
            dataSet.UpdatedAt = DateTime.Now;
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