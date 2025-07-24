using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using InventorySystem.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// 既存データのDataSetId不整合修復サービス
    /// Process 2-5のDataSetId不整合問題を解決するため、既存データを修復する
    /// </summary>
    public class DataSetIdRepairService
    {
        private readonly string _connectionString;
        private readonly ILogger<DataSetIdRepairService> _logger;
        private readonly IDataSetIdManager _dataSetIdManager;

        public DataSetIdRepairService(
            IConfiguration configuration,
            ILogger<DataSetIdRepairService> logger,
            IDataSetIdManager dataSetIdManager)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("DefaultConnection not found");
            _logger = logger;
            _dataSetIdManager = dataSetIdManager;
        }

        /// <summary>
        /// 既存データのDataSetId不整合を修復
        /// </summary>
        /// <param name="targetDate">修復対象日付</param>
        /// <returns>修復結果</returns>
        public async Task<DataSetIdRepairResult> RepairDataSetIdInconsistenciesAsync(DateTime targetDate)
        {
            _logger.LogInformation("DataSetId不整合修復開始: TargetDate={TargetDate}", targetDate.ToString("yyyy-MM-dd"));

            var result = new DataSetIdRepairResult
            {
                TargetDate = targetDate,
                StartTime = DateTime.Now
            };

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // 1. 既存の売上伝票DataSetIdを確認・修復
                result.SalesVoucherResult = await RepairSalesVoucherDataSetIdAsync(connection, targetDate);

                // 2. 既存のCP在庫マスタDataSetIdを確認・修復
                result.CpInventoryResult = await RepairCpInventoryDataSetIdAsync(connection, targetDate);

                // 3. 他の伝票データも修復（必要に応じて）
                result.PurchaseVoucherResult = await RepairPurchaseVoucherDataSetIdAsync(connection, targetDate);
                result.InventoryAdjustmentResult = await RepairInventoryAdjustmentDataSetIdAsync(connection, targetDate);

                result.Success = true;
                result.EndTime = DateTime.Now;

                _logger.LogInformation("DataSetId不整合修復完了: TargetDate={TargetDate}, 処理時間={ElapsedMs}ms", 
                    targetDate.ToString("yyyy-MM-dd"), (result.EndTime - result.StartTime).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.EndTime = DateTime.Now;

                _logger.LogError(ex, "DataSetId不整合修復でエラーが発生: TargetDate={TargetDate}", 
                    targetDate.ToString("yyyy-MM-dd"));
                throw;
            }

            return result;
        }

        /// <summary>
        /// 売上伝票のDataSetId修復
        /// </summary>
        private async Task<TableRepairResult> RepairSalesVoucherDataSetIdAsync(IDbConnection connection, DateTime targetDate)
        {
            _logger.LogInformation("売上伝票DataSetId修復開始: TargetDate={TargetDate}", targetDate.ToString("yyyy-MM-dd"));

            var result = new TableRepairResult { TableName = "SalesVouchers" };

            // 現在のDataSetIdを確認（NULLも含める）
            const string checkSql = @"
                SELECT ISNULL(DataSetId, 'NULL') as DataSetId, COUNT(*) as RecordCount 
                FROM SalesVouchers 
                WHERE JobDate = @JobDate 
                GROUP BY DataSetId";

            var currentDataSets = await connection.QueryAsync(checkSql, new { JobDate = targetDate });
            result.BeforeDataSetIds = currentDataSets.Select(x => new DataSetIdInfo { DataSetId = x.DataSetId, Count = x.RecordCount }).ToList();

            _logger.LogInformation("売上伝票の現在のDataSetId: {DataSets}", 
                string.Join(", ", result.BeforeDataSetIds.Select(x => $"{x.DataSetId}({x.Count}件)")));

            if (!result.BeforeDataSetIds.Any())
            {
                _logger.LogWarning("対象日付の売上伝票が見つかりません: JobDate={JobDate}", targetDate);
                return result;
            }

            // DataSetIdManagerから正しいDataSetIdを取得
            var correctDataSetId = await _dataSetIdManager.GetOrCreateDataSetIdAsync(targetDate, "SalesVoucher");

            // 不整合なレコードを修復（NULL値も含む）
            const string updateSql = @"
                UPDATE SalesVouchers 
                SET DataSetId = @CorrectDataSetId
                WHERE JobDate = @JobDate AND (DataSetId != @CorrectDataSetId OR DataSetId IS NULL)";

            result.UpdatedRecords = await connection.ExecuteAsync(updateSql, new 
            { 
                JobDate = targetDate, 
                CorrectDataSetId = correctDataSetId 
            });

            // 修復後の状態を確認
            var afterDataSets = await connection.QueryAsync(checkSql, new { JobDate = targetDate });
            result.AfterDataSetIds = afterDataSets.Select(x => new DataSetIdInfo { DataSetId = x.DataSetId, Count = x.RecordCount }).ToList();
            result.CorrectDataSetId = correctDataSetId;

            _logger.LogInformation("売上伝票DataSetId修復完了: 更新件数={UpdatedRecords}, 正しいDataSetId={CorrectDataSetId}", 
                result.UpdatedRecords, correctDataSetId);

            return result;
        }

        /// <summary>
        /// CP在庫マスタのDataSetId修復
        /// </summary>
        private async Task<TableRepairResult> RepairCpInventoryDataSetIdAsync(IDbConnection connection, DateTime targetDate)
        {
            _logger.LogInformation("CP在庫マスタDataSetId修復開始: TargetDate={TargetDate}", targetDate.ToString("yyyy-MM-dd"));

            var result = new TableRepairResult { TableName = "CPInventoryMaster" };

            // 現在のDataSetIdを確認（NULLも含める）
            const string checkSql = @"
                SELECT ISNULL(DataSetId, 'NULL') as DataSetId, COUNT(*) as RecordCount 
                FROM CPInventoryMaster 
                WHERE JobDate = @JobDate 
                GROUP BY DataSetId";

            var currentDataSets = await connection.QueryAsync(checkSql, new { JobDate = targetDate });
            result.BeforeDataSetIds = currentDataSets.Select(x => new DataSetIdInfo { DataSetId = x.DataSetId, Count = x.RecordCount }).ToList();

            if (!result.BeforeDataSetIds.Any())
            {
                _logger.LogWarning("対象日付のCP在庫マスタが見つかりません: JobDate={JobDate}", targetDate);
                return result;
            }

            // DataSetIdManagerから正しいDataSetIdを取得
            var correctDataSetId = await _dataSetIdManager.GetOrCreateDataSetIdAsync(targetDate, "CpInventoryMaster");

            // 不整合なレコードを修復（CPInventoryMasterテーブルにはUpdatedDateカラムを使用、NULL値も含む）
            const string updateSql = @"
                UPDATE CPInventoryMaster 
                SET DataSetId = @CorrectDataSetId, UpdatedDate = GETDATE()
                WHERE JobDate = @JobDate AND (DataSetId != @CorrectDataSetId OR DataSetId IS NULL)";

            result.UpdatedRecords = await connection.ExecuteAsync(updateSql, new 
            { 
                JobDate = targetDate, 
                CorrectDataSetId = correctDataSetId 
            });

            // 修復後の状態を確認
            var afterDataSets = await connection.QueryAsync(checkSql, new { JobDate = targetDate });
            result.AfterDataSetIds = afterDataSets.Select(x => new DataSetIdInfo { DataSetId = x.DataSetId, Count = x.RecordCount }).ToList();
            result.CorrectDataSetId = correctDataSetId;

            _logger.LogInformation("CP在庫マスタDataSetId修復完了: 更新件数={UpdatedRecords}, 正しいDataSetId={CorrectDataSetId}", 
                result.UpdatedRecords, correctDataSetId);

            return result;
        }

        /// <summary>
        /// 仕入伝票のDataSetId修復
        /// </summary>
        private async Task<TableRepairResult> RepairPurchaseVoucherDataSetIdAsync(IDbConnection connection, DateTime targetDate)
        {
            var result = new TableRepairResult { TableName = "PurchaseVouchers" };

            // 仕入伝票の修復ロジック（NULLも含める）
            const string checkSql = @"
                SELECT ISNULL(DataSetId, 'NULL') as DataSetId, COUNT(*) as RecordCount 
                FROM PurchaseVouchers 
                WHERE JobDate = @JobDate 
                GROUP BY DataSetId";

            var currentDataSets = await connection.QueryAsync(checkSql, new { JobDate = targetDate });
            result.BeforeDataSetIds = currentDataSets.Select(x => new DataSetIdInfo { DataSetId = x.DataSetId, Count = x.RecordCount }).ToList();

            if (result.BeforeDataSetIds.Any())
            {
                var correctDataSetId = await _dataSetIdManager.GetOrCreateDataSetIdAsync(targetDate, "PurchaseVoucher");

                const string updateSql = @"
                    UPDATE PurchaseVouchers 
                    SET DataSetId = @CorrectDataSetId
                    WHERE JobDate = @JobDate AND (DataSetId != @CorrectDataSetId OR DataSetId IS NULL)";

                result.UpdatedRecords = await connection.ExecuteAsync(updateSql, new 
                { 
                    JobDate = targetDate, 
                    CorrectDataSetId = correctDataSetId 
                });

                // 修復後の状態を確認
                var afterDataSets = await connection.QueryAsync(checkSql, new { JobDate = targetDate });
                result.AfterDataSetIds = afterDataSets.Select(x => new DataSetIdInfo { DataSetId = x.DataSetId, Count = x.RecordCount }).ToList();
                result.CorrectDataSetId = correctDataSetId;

                _logger.LogInformation("仕入伝票DataSetId修復完了: 更新件数={UpdatedRecords}, 正しいDataSetId={CorrectDataSetId}", 
                    result.UpdatedRecords, correctDataSetId);
            }
            else
            {
                _logger.LogInformation("対象日付の仕入伝票が見つかりません: JobDate={JobDate}", targetDate);
            }

            return result;
        }

        /// <summary>
        /// 在庫調整のDataSetId修復
        /// </summary>
        private async Task<TableRepairResult> RepairInventoryAdjustmentDataSetIdAsync(IDbConnection connection, DateTime targetDate)
        {
            var result = new TableRepairResult { TableName = "InventoryAdjustments" };

            // 在庫調整の修復ロジック（NULLも含める）
            const string checkSql = @"
                SELECT ISNULL(DataSetId, 'NULL') as DataSetId, COUNT(*) as RecordCount 
                FROM InventoryAdjustments 
                WHERE JobDate = @JobDate 
                GROUP BY DataSetId";

            var currentDataSets = await connection.QueryAsync(checkSql, new { JobDate = targetDate });
            result.BeforeDataSetIds = currentDataSets.Select(x => new DataSetIdInfo { DataSetId = x.DataSetId, Count = x.RecordCount }).ToList();

            if (result.BeforeDataSetIds.Any())
            {
                var correctDataSetId = await _dataSetIdManager.GetOrCreateDataSetIdAsync(targetDate, "InventoryAdjustment");

                const string updateSql = @"
                    UPDATE InventoryAdjustments 
                    SET DataSetId = @CorrectDataSetId
                    WHERE JobDate = @JobDate AND (DataSetId != @CorrectDataSetId OR DataSetId IS NULL)";

                result.UpdatedRecords = await connection.ExecuteAsync(updateSql, new 
                { 
                    JobDate = targetDate, 
                    CorrectDataSetId = correctDataSetId 
                });

                // 修復後の状態を確認
                var afterDataSets = await connection.QueryAsync(checkSql, new { JobDate = targetDate });
                result.AfterDataSetIds = afterDataSets.Select(x => new DataSetIdInfo { DataSetId = x.DataSetId, Count = x.RecordCount }).ToList();
                result.CorrectDataSetId = correctDataSetId;

                _logger.LogInformation("在庫調整DataSetId修復完了: 更新件数={UpdatedRecords}, 正しいDataSetId={CorrectDataSetId}", 
                    result.UpdatedRecords, correctDataSetId);
            }
            else
            {
                _logger.LogInformation("対象日付の在庫調整が見つかりません: JobDate={JobDate}", targetDate);
            }

            return result;
        }
    }

    /// <summary>
    /// DataSetId修復結果
    /// </summary>
    public class DataSetIdRepairResult
    {
        public DateTime TargetDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TableRepairResult SalesVoucherResult { get; set; } = new();
        public TableRepairResult CpInventoryResult { get; set; } = new();
        public TableRepairResult PurchaseVoucherResult { get; set; } = new();
        public TableRepairResult InventoryAdjustmentResult { get; set; } = new();

        public TimeSpan ElapsedTime => EndTime - StartTime;
        public int TotalUpdatedRecords => SalesVoucherResult.UpdatedRecords + CpInventoryResult.UpdatedRecords + 
                                         PurchaseVoucherResult.UpdatedRecords + InventoryAdjustmentResult.UpdatedRecords;
    }

    /// <summary>
    /// テーブル別修復結果
    /// </summary>
    public class TableRepairResult
    {
        public string TableName { get; set; } = string.Empty;
        public List<DataSetIdInfo> BeforeDataSetIds { get; set; } = new();
        public List<DataSetIdInfo> AfterDataSetIds { get; set; } = new();
        public string? CorrectDataSetId { get; set; }
        public int UpdatedRecords { get; set; }
    }

    /// <summary>
    /// DataSetId情報
    /// </summary>
    public class DataSetIdInfo
    {
        public string DataSetId { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}