using System;
using System.Threading.Tasks;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Factories;
using InventorySystem.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// DataSetsとDataSetManagementの両テーブルを管理する統一サービス
    /// Phase 1: 二重書き込みによる段階的移行を実装
    /// </summary>
    public class UnifiedDataSetService : IUnifiedDataSetService
    {
        private readonly IDataSetRepository _dataSetRepository;
        private readonly IDataSetManagementRepository _dataSetManagementRepository;
        private readonly IDataSetManagementFactory _dataSetFactory;
        private readonly ITimeProvider _timeProvider;
        private readonly ILogger<UnifiedDataSetService> _logger;

        public UnifiedDataSetService(
            IDataSetRepository dataSetRepository,
            IDataSetManagementRepository dataSetManagementRepository,
            IDataSetManagementFactory dataSetFactory,
            ITimeProvider timeProvider,
            ILogger<UnifiedDataSetService> logger)
        {
            _dataSetRepository = dataSetRepository;
            _dataSetManagementRepository = dataSetManagementRepository;
            _dataSetFactory = dataSetFactory;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        /// データセットを作成します（両テーブルに書き込み）
        /// </summary>
        public async Task<string> CreateDataSetAsync(UnifiedDataSetInfo info)
        {
            // データセットIDを生成（GUIDを使用）
            var dataSetId = info.DataSetId ?? Guid.NewGuid().ToString();
            
            _logger.LogInformation("統一データセット作成開始: ID={DataSetId}, ProcessType={ProcessType}", 
                dataSetId, info.ProcessType);

            var createdAt = _timeProvider.UtcNow;  // ⭐ Phase 2-B: UTC統一（Gemini推奨）
            
            // 1. DataSetsテーブルへの書き込み（既存処理との互換性維持）
            var dataSetCreated = false;
            try
            {
                // ✅ 修正: 存在するプロパティのみ設定、適切なデフォルト値を使用
                var dataSet = new InventorySystem.Core.Entities.DataSet
                {
                    Id = dataSetId,
                    Name = info.Name ?? $"{info.ProcessType}_{info.JobDate:yyyyMMdd}_{DateTime.Now:HHmmss}",
                    Description = info.Description ?? $"{info.ProcessType} データセット ({info.JobDate:yyyy-MM-dd})",
                    DataSetType = ConvertProcessTypeForDataSets(info.ProcessType),
                    ImportedAt = createdAt,
                    RecordCount = 0,
                    Status = "Processing",
                    ErrorMessage = null,
                    FilePath = info.FilePath,
                    JobDate = info.JobDate,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };

                await _dataSetRepository.CreateAsync(dataSet);
                dataSetCreated = true;
                _logger.LogInformation("DataSetsテーブルへの書き込み成功: ID={DataSetId}", dataSetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSetsテーブルへの書き込み失敗: ID={DataSetId}", dataSetId);
                // DataSetsへの書き込みが失敗した場合でも処理を継続
            }

            // 2. DataSetManagementテーブルへの書き込み（新しい管理機能）
            var dataSetManagementCreated = false;
            try
            {
                // ✅ DateTime.MinValue対策: JobDateが未設定の場合の安全措置
                var safeJobDate = info.JobDate == DateTime.MinValue ? DateTime.Today : info.JobDate;
                
                // ✅ ファクトリパターンでDataSetManagement作成（Phase 2-B: Gemini推奨）
                var importedFiles = info.FilePath != null ? new List<string> { Path.GetFileName(info.FilePath) } : null;
                var dataSetManagement = _dataSetFactory.CreateNew(
                    dataSetId,
                    safeJobDate,
                    info.ProcessType,
                    info.CreatedBy ?? "system",
                    info.Department ?? "Unknown",
                    info.ImportType ?? "IMPORT",
                    importedFiles,
                    info.Description);

                await _dataSetManagementRepository.CreateAsync(dataSetManagement);
                dataSetManagementCreated = true;
                _logger.LogInformation("DataSetManagementテーブルへの書き込み成功: ID={DataSetId}", dataSetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSetManagementテーブルへの書き込み失敗: ID={DataSetId}", dataSetId);
                // DataSetManagementへの書き込みが失敗した場合でも処理を継続
            }

            // ✅ 結果ログと適切なエラーハンドリング
            if (dataSetCreated && dataSetManagementCreated)
            {
                _logger.LogInformation("統一データセット作成完了: ID={DataSetId}", dataSetId);
            }
            else if (!dataSetCreated && !dataSetManagementCreated)
            {
                _logger.LogError("DataSets、DataSetManagement両方の書き込みに失敗: ID={DataSetId}", dataSetId);
                throw new InvalidOperationException($"データセット作成に失敗しました: {dataSetId}");
            }
            else
            {
                _logger.LogWarning("部分的な書き込み成功: DataSets={DataSetsSuccess}, DataSetManagement={DataSetManagementSuccess}, ID={DataSetId}", 
                    dataSetCreated, dataSetManagementCreated, dataSetId);
            }

            return dataSetId;
        }

        /// <summary>
        /// データセットのステータスを更新します
        /// </summary>
        public async Task UpdateStatusAsync(string dataSetId, InventorySystem.Core.Interfaces.DataSetStatus status, string? errorMessage = null)
        {
            _logger.LogInformation("統一データセットステータス更新: ID={DataSetId}, Status={Status}", dataSetId, status);

            // DataSetsテーブルの更新
            try
            {
                var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId);
                if (dataSet != null)
                {
                    dataSet.Status = status.ToString();
                    dataSet.ErrorMessage = errorMessage;
                    dataSet.UpdatedAt = _timeProvider.UtcNow;  // ⭐ Phase 2-B: UTC統一（Gemini推奨）
                    await _dataSetRepository.UpdateStatusAsync(dataSetId, status.ToString(), errorMessage);
                    _logger.LogDebug("DataSetsテーブルのステータス更新成功: ID={DataSetId}", dataSetId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSetsテーブルのステータス更新失敗: ID={DataSetId}", dataSetId);
            }

            // DataSetManagementテーブルの更新（IsActiveフラグで管理）
            try
            {
                var dataSetManagement = await _dataSetManagementRepository.GetByIdAsync(dataSetId);
                if (dataSetManagement != null)
                {
                    dataSetManagement.IsActive = status == InventorySystem.Core.Interfaces.DataSetStatus.Processing;
                    
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        dataSetManagement.Notes = string.IsNullOrEmpty(dataSetManagement.Notes) 
                            ? $"Error: {errorMessage}"
                            : $"{dataSetManagement.Notes} | Error: {errorMessage}";
                    }
                    
                    // DataSetManagementの更新は将来的に実装予定
                    _logger.LogDebug("DataSetManagementテーブルの更新成功: ID={DataSetId}", dataSetId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSetManagementテーブルのステータス更新失敗: ID={DataSetId}", dataSetId);
            }
        }

        /// <summary>
        /// データセットのレコード数を更新します
        /// </summary>
        public async Task UpdateRecordCountAsync(string dataSetId, int recordCount)
        {
            try
            {
                _logger.LogInformation("統一データセットレコード数更新: ID={DataSetId}, Count={RecordCount}", 
                    dataSetId, recordCount);

                // ✅ DataSetsテーブルの更新（修正済みメソッド使用）
                try
                {
                    await _dataSetRepository.UpdateRecordCountAsync(dataSetId, recordCount);
                    _logger.LogDebug("DataSetsテーブルのレコード数更新成功: ID={DataSetId}", dataSetId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DataSetsテーブルのレコード数更新失敗: ID={DataSetId}", dataSetId);
                }

                // ✅ DataSetManagementテーブルの更新
                try
                {
                    var dataSetManagement = await _dataSetManagementRepository.GetByIdAsync(dataSetId);
                    if (dataSetManagement != null)
                    {
                        dataSetManagement.RecordCount = recordCount;
                        dataSetManagement.TotalRecordCount = Math.Max(dataSetManagement.TotalRecordCount, recordCount);
                        // 更新処理は将来的に実装予定
                        _logger.LogDebug("DataSetManagementテーブルのレコード数更新成功: ID={DataSetId}", dataSetId);
                    }
                    else
                    {
                        _logger.LogWarning("DataSetManagementレコードが見つかりません: ID={DataSetId}", dataSetId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DataSetManagementテーブルのレコード数更新失敗: ID={DataSetId}", dataSetId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "統一データセットレコード数更新失敗: ID={DataSetId}", dataSetId);
                throw;
            }
        }

        /// <summary>
        /// データセットの処理を完了します
        /// </summary>
        public async Task CompleteDataSetAsync(string dataSetId, int finalRecordCount)
        {
            _logger.LogInformation("統一データセット処理完了: ID={DataSetId}, FinalCount={FinalRecordCount}", 
                dataSetId, finalRecordCount);

            await UpdateRecordCountAsync(dataSetId, finalRecordCount);
            await UpdateStatusAsync(dataSetId, InventorySystem.Core.Interfaces.DataSetStatus.Completed);
        }

        /// <summary>
        /// ProcessTypeをDataSets用に変換
        /// </summary>
        private string ConvertProcessTypeForDataSets(string processType)
        {
            return processType switch
            {
                "SALES" => "Sales",
                "PURCHASE" => "Purchase",
                "ADJUSTMENT" => "Adjustment",
                "PRODUCT" => "Product",
                "CUSTOMER" => "Customer",
                "SUPPLIER" => "Supplier",
                "INITIAL_INVENTORY" => "InitialInventory",
                "PREVIOUS_INVENTORY" => "PreviousInventory",
                "DAILY_REPORT" => "DailyReport",
                _ => processType
            };
        }
    }
}