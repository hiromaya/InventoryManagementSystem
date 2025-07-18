using InventorySystem.Core.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventorySystem.Import.Services
{
    /// <summary>
    /// UnifiedDataSetServiceをIDataSetServiceインターフェースに適合させるラッパー
    /// フィーチャーフラグがOFFの場合に使用される
    /// </summary>
    public class LegacyDataSetService : IDataSetService
    {
        private readonly IUnifiedDataSetService _unifiedDataSetService;
        private readonly IDataSetRepository _dataSetRepository;
        private readonly ILogger<LegacyDataSetService> _logger;
        private readonly FeatureFlags _features;
        
        public LegacyDataSetService(
            IUnifiedDataSetService unifiedDataSetService,
            IDataSetRepository dataSetRepository,
            ILogger<LegacyDataSetService> logger,
            IOptions<FeatureFlags> features)
        {
            _unifiedDataSetService = unifiedDataSetService;
            _dataSetRepository = dataSetRepository;
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
            var info = new UnifiedDataSetInfo
            {
                Name = name,
                ProcessType = processType,
                JobDate = jobDate,
                Description = description,
                FilePath = filePath,
                ImportType = "IMPORT",
                CreatedBy = "system",
                Department = "DeptA"
            };
            
            var dataSetId = await _unifiedDataSetService.CreateDataSetAsync(info);
            
            if (_features.EnableDataSetsMigrationLog)
            {
                _logger.LogInformation(
                    "LegacyDataSetService: データセット作成 Id={DataSetId}, ProcessType={ProcessType}",
                    dataSetId, processType);
            }
            
            return dataSetId;
        }
        
        public async Task UpdateStatusAsync(string dataSetId, string status)
        {
            // ステータス文字列を列挙型に変換
            var dataSetStatus = status switch
            {
                "Processing" => DataSetStatus.Processing,
                "Completed" => DataSetStatus.Completed,
                "Imported" => DataSetStatus.Completed,
                "Error" => DataSetStatus.Failed,
                "Failed" => DataSetStatus.Failed,
                _ => DataSetStatus.Processing
            };
            
            await _unifiedDataSetService.UpdateStatusAsync(dataSetId, dataSetStatus);
            
            if (_features.EnableDataSetsMigrationLog)
            {
                _logger.LogInformation(
                    "LegacyDataSetService: ステータス更新 Id={DataSetId}, Status={Status}",
                    dataSetId, status);
            }
        }
        
        public async Task UpdateRecordCountAsync(string dataSetId, int recordCount)
        {
            await _unifiedDataSetService.UpdateRecordCountAsync(dataSetId, recordCount);
        }
        
        public async Task SetErrorAsync(string dataSetId, string errorMessage)
        {
            await _unifiedDataSetService.UpdateStatusAsync(
                dataSetId, 
                DataSetStatus.Failed, 
                errorMessage);
                
            if (_features.EnableDataSetsMigrationLog)
            {
                _logger.LogInformation(
                    "LegacyDataSetService: エラー設定 Id={DataSetId}, Error={Error}",
                    dataSetId, errorMessage);
            }
        }
        
        public async Task<bool> ExistsAsync(string dataSetId)
        {
            var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId);
            return dataSet != null;
        }
        
        public async Task<DataSetInfo?> GetByIdAsync(string dataSetId)
        {
            var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId);
            if (dataSet == null) return null;
            
            return new DataSetInfo
            {
                Id = dataSet.Id,
                Name = dataSet.Name,
                ProcessType = dataSet.ProcessType,
                Status = dataSet.Status,
                JobDate = dataSet.JobDate,
                RecordCount = dataSet.RecordCount,
                ErrorMessage = dataSet.ErrorMessage,
                CreatedAt = dataSet.CreatedAt,
                UpdatedAt = dataSet.UpdatedAt,
                IsActive = dataSet.Status == "Completed" || dataSet.Status == "Imported",
                FilePath = dataSet.FilePath,
                Description = dataSet.Description
            };
        }
        
        public async Task UpdateTimestampAsync(string dataSetId)
        {
            var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId);
            if (dataSet == null) return;
            
            dataSet.UpdatedAt = DateTime.Now;
            await _dataSetRepository.UpdateAsync(dataSet);
        }
    }
}