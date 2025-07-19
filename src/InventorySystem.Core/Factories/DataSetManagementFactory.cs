using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Core.Factories
{
    /// <summary>
    /// DataSetManagementエンティティの生成を担当するファクトリ実装
    /// Gemini推奨：時刻依存性を外部から注入し、テスト容易性を向上
    /// </summary>
    public class DataSetManagementFactory : IDataSetManagementFactory
    {
        private readonly ITimeProvider _timeProvider;
        private readonly ILogger<DataSetManagementFactory> _logger;

        public DataSetManagementFactory(
            ITimeProvider timeProvider,
            ILogger<DataSetManagementFactory> logger)
        {
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        /// 新しいDataSetManagementエンティティを作成します
        /// UTC時刻でCreatedAt/UpdatedAtを統一設定
        /// </summary>
        public DataSetManagement CreateNew(
            string dataSetId,
            DateTime jobDate,
            string processType,
            string createdBy = "System",
            string department = "DeptA",
            string? importType = null,
            List<string>? importedFiles = null,
            string? notes = null)
        {
            var currentTime = _timeProvider.UtcNow;  // Gemini推奨：UTC統一
            
            // インポート種別の自動判定
            var resolvedImportType = importType ?? processType switch
            {
                "IMPORT" => "IMPORT",
                "CARRYOVER" => "CARRYOVER", 
                "INIT" => "INIT",
                "MANUAL" => "MANUAL",
                _ => "UNKNOWN"
            };

            var dataSetManagement = new DataSetManagement
            {
                DataSetId = dataSetId,
                JobDate = jobDate,
                ProcessType = processType,
                ImportType = resolvedImportType,
                RecordCount = 0,  // 初期値、後でサービス層で更新
                TotalRecordCount = 0,
                IsActive = true,
                IsArchived = false,
                ParentDataSetId = null,  // 必要に応じて後で設定
                ImportedFiles = importedFiles != null ? JsonSerializer.Serialize(importedFiles) : null,
                CreatedAt = currentTime,  // ⭐ UTC時刻で統一
                UpdatedAt = currentTime,  // ⭐ UTC時刻で統一
                CreatedBy = createdBy,
                Department = department,
                Notes = notes,
                // 拡張フィールド（Phase 1で追加されたもの）
                Name = $"{processType}_{jobDate:yyyyMMdd}_{currentTime:HHmmss}",
                Description = $"{processType} データセット ({jobDate:yyyy-MM-dd})",
                FilePath = null,  // 必要に応じて後で設定
                Status = "Processing",
                ErrorMessage = null
            };

            _logger.LogDebug("DataSetManagement作成: Id={DataSetId}, ProcessType={ProcessType}, UTC={UtcTime}",
                dataSetId, processType, currentTime);

            return dataSetManagement;
        }

        /// <summary>
        /// 繰越処理用のDataSetManagementエンティティを作成します
        /// </summary>
        public DataSetManagement CreateForCarryover(
            string dataSetId,
            DateTime targetDate,
            string department,
            int recordCount,
            string? parentDataSetId = null,
            string? notes = null)
        {
            var currentTime = _timeProvider.UtcNow;

            var dataSetManagement = new DataSetManagement
            {
                DataSetId = dataSetId,
                JobDate = targetDate,
                ProcessType = "CARRYOVER",
                ImportType = "CARRYOVER",
                RecordCount = recordCount,
                TotalRecordCount = recordCount,
                IsActive = true,
                IsArchived = false,
                ParentDataSetId = parentDataSetId,
                ImportedFiles = null,  // 繰越処理にはファイルがない
                CreatedAt = currentTime,
                UpdatedAt = currentTime,
                CreatedBy = "System",
                Department = department,
                Notes = notes ?? $"前日在庫繰越処理: {recordCount}件",
                // 拡張フィールド
                Name = $"CARRYOVER_{targetDate:yyyyMMdd}_{currentTime:HHmmss}",
                Description = $"前日在庫繰越処理 ({targetDate:yyyy-MM-dd})",
                FilePath = null,
                Status = "Processing",
                ErrorMessage = null
            };

            _logger.LogInformation("繰越用DataSetManagement作成: Id={DataSetId}, Date={Date}, Count={Count}",
                dataSetId, targetDate, recordCount);

            return dataSetManagement;
        }

        /// <summary>
        /// 既存のDataSetManagementエンティティのUpdatedAtを更新します
        /// </summary>
        public void UpdateTimestamp(DataSetManagement dataSetManagement)
        {
            if (dataSetManagement == null)
                throw new ArgumentNullException(nameof(dataSetManagement));

            dataSetManagement.UpdatedAt = _timeProvider.UtcNow;

            _logger.LogTrace("DataSetManagement UpdatedAt更新: Id={DataSetId}, UTC={UtcTime}",
                dataSetManagement.DataSetId, dataSetManagement.UpdatedAt);
        }
    }
}