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
            string? filePath = null,
            string? predefinedDataSetId = null)
        {
            var dataSetId = predefinedDataSetId ?? Guid.NewGuid().ToString();
            
            // 既存のDataSetIdが指定されている場合、既にDataSetManagementレコードが存在するかチェック
            if (!string.IsNullOrEmpty(predefinedDataSetId))
            {
                var existingDataSet = await _repository.GetByIdAsync(dataSetId);
                if (existingDataSet != null)
                {
                    // 既にレコードが存在する場合
                    _logger.LogInformation(
                        "既存のDataSetManagementレコードが見つかりました。新規作成をスキップします: " +
                        "Id={DataSetId}, ProcessType={ProcessType}, JobDate={JobDate}, Status={Status}, IsActive={IsActive}",
                        dataSetId, existingDataSet.ProcessType, existingDataSet.JobDate, 
                        existingDataSet.Status, existingDataSet.IsActive);
                    
                    // ステータスがCompletedまたはErrorの場合は警告
                    if (existingDataSet.Status == "Completed" || existingDataSet.Status == "Error")
                    {
                        _logger.LogWarning(
                            "既存のDataSetは既に処理済みです: Status={Status}. " +
                            "同じJobDate+ProcessTypeで再実行する場合は、新しいDataSetIdの生成を検討してください。",
                            existingDataSet.Status);
                    }
                    
                    return dataSetId;
                }
            }
            
            // 以下、既存の新規作成処理
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

        /// <summary>
        /// 指定されたJobDateとProcessTypeの古いDataSetを無効化
        /// </summary>
        public async Task DeactivateOldDataSetsAsync(DateTime jobDate, string processType, string currentDataSetId)
        {
            _logger.LogInformation(
                "古いDataSetの無効化開始: JobDate={JobDate}, ProcessType={ProcessType}, CurrentDataSetId={CurrentDataSetId}",
                jobDate, processType, currentDataSetId);
            
            // 同じJobDate+ProcessTypeで、現在のDataSetId以外のアクティブなレコードを取得
            var oldDataSets = await _repository.GetByJobDateAndTypeAsync(jobDate, processType);
            var toDeactivate = oldDataSets
                .Where(ds => ds.IsActive && ds.DataSetId != currentDataSetId)
                .ToList();
            
            foreach (var dataSet in toDeactivate)
            {
                dataSet.IsActive = false;
                dataSet.DeactivatedAt = _timeProvider.Now.DateTime;
                dataSet.DeactivatedBy = "SYSTEM_AUTO_DEACTIVATION";
                dataSet.Status = dataSet.Status == "Processing" ? "Cancelled" : dataSet.Status;
                dataSet.Notes = (dataSet.Notes ?? "") + 
                    $" | Auto-deactivated on {_timeProvider.Now.DateTime:yyyy-MM-dd HH:mm:ss} due to new import";
                
                await _repository.UpdateAsync(dataSet);
                
                _logger.LogInformation(
                    "DataSet無効化完了: DataSetId={DataSetId}, ProcessType={ProcessType}",
                    dataSet.DataSetId, dataSet.ProcessType);
            }
            
            _logger.LogInformation(
                "無効化完了: {Count}件のDataSetを無効化しました",
                toDeactivate.Count);
        }
    }
}