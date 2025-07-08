using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces.Development;

namespace InventorySystem.Data.Services.Development;

/// <summary>
/// 日次終了処理リセットサービス
/// </summary>
public class DailyCloseResetService : IDailyCloseResetService
{
    private readonly string _connectionString;
    private readonly ILogger<DailyCloseResetService> _logger;
    
    public DailyCloseResetService(string connectionString, ILogger<DailyCloseResetService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<ResetResult> ResetDailyCloseAsync(DateTime jobDate, bool resetAll = false)
    {
        var result = new ResetResult();
        
        try
        {
            _logger.LogWarning("日次終了処理のリセットを開始します。JobDate={JobDate}, ResetAll={ResetAll}", 
                jobDate, resetAll);
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // トランザクション開始
            using var transaction = connection.BeginTransaction();
            
            try
            {
                // 1. DailyCloseManagementから削除
                var deleteDailyCloseSql = @"
                    DELETE FROM DailyCloseManagement 
                    WHERE JobDate = @JobDate";
                
                result.DeletedDailyCloseRecords = await connection.ExecuteAsync(
                    deleteDailyCloseSql, 
                    new { JobDate = jobDate }, 
                    transaction);
                
                _logger.LogInformation("DailyCloseManagement: {Count}件削除", result.DeletedDailyCloseRecords);
                
                // 2. ProcessHistoryから削除
                var deleteProcessHistorySql = @"
                    DELETE FROM ProcessHistory 
                    WHERE JobDate = @JobDate 
                        AND ProcessType IN ('DAILY_CLOSE', 'DAILY_REPORT')";
                
                result.DeletedProcessHistoryRecords = await connection.ExecuteAsync(
                    deleteProcessHistorySql, 
                    new { JobDate = jobDate }, 
                    transaction);
                
                _logger.LogInformation("ProcessHistory: {Count}件削除", result.DeletedProcessHistoryRecords);
                
                // 3. AuditLogsから削除（開発環境のみ）
                var deleteAuditLogsSql = @"
                    DELETE FROM AuditLogs 
                    WHERE JobDate = @JobDate 
                        AND ProcessType IN ('DAILY_CLOSE', 'DAILY_REPORT')";
                
                result.DeletedAuditLogs = await connection.ExecuteAsync(
                    deleteAuditLogsSql, 
                    new { JobDate = jobDate }, 
                    transaction);
                
                _logger.LogInformation("AuditLogs: {Count}件削除", result.DeletedAuditLogs);
                
                // 4. --allオプション時：在庫マスタのリセット
                if (resetAll)
                {
                    // 在庫マスタのCurrentStockをリセット
                    var resetInventorySql = @"
                        UPDATE InventoryMaster 
                        SET CurrentStock = 0, 
                            CurrentStockAmount = 0,
                            UpdatedDate = GETDATE()
                        WHERE JobDate = @JobDate";
                    
                    result.ResetInventoryRecords = await connection.ExecuteAsync(
                        resetInventorySql, 
                        new { JobDate = jobDate }, 
                        transaction);
                    
                    _logger.LogWarning("InventoryMaster: {Count}件の在庫をリセット", result.ResetInventoryRecords);
                    
                    // 翌日以降のデータが存在する場合は警告
                    var hasNextDayData = await CheckNextDayDataAsync(connection, jobDate, transaction);
                    if (hasNextDayData)
                    {
                        _logger.LogWarning("警告: {JobDate}の翌日以降にデータが存在します。整合性に注意してください。", jobDate);
                        result.Message = "翌日以降のデータが存在します。整合性に注意してください。";
                    }
                }
                
                // コミット
                await transaction.CommitAsync();
                
                result.Success = true;
                if (string.IsNullOrEmpty(result.Message))
                {
                    result.Message = "リセット処理が正常に完了しました。";
                }
                
                _logger.LogInformation("日次終了処理のリセットが完了しました");
            }
            catch (Exception ex)
            {
                // ロールバック
                await transaction.RollbackAsync();
                
                _logger.LogError(ex, "リセット処理中にエラーが発生しました");
                result.Success = false;
                result.Message = $"エラー: {ex.Message}";
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "リセット処理で予期しないエラーが発生しました");
            result.Success = false;
            result.Message = $"予期しないエラー: {ex.Message}";
        }
        
        return result;
    }
    
    public async Task<bool> CanResetAsync(DateTime jobDate)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            // 日次終了処理が実行されているか確認
            var hasDailyClose = await connection.ExecuteScalarAsync<bool>(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM DailyCloseManagement WHERE JobDate = @JobDate) THEN 1 ELSE 0 END",
                new { JobDate = jobDate });
            
            return hasDailyClose;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "リセット可能性の確認でエラーが発生しました");
            return false;
        }
    }
    
    public async Task<RelatedDataStatus> GetRelatedDataStatusAsync(DateTime jobDate)
    {
        var status = new RelatedDataStatus();
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            // DailyCloseManagementの確認
            var dailyCloseInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT ProcessedAt, ProcessedBy 
                FROM DailyCloseManagement 
                WHERE JobDate = @JobDate",
                new { JobDate = jobDate });
            
            if (dailyCloseInfo != null)
            {
                status.HasDailyCloseRecord = true;
                status.LastDailyCloseAt = dailyCloseInfo.ProcessedAt;
                status.LastProcessedBy = dailyCloseInfo.ProcessedBy;
            }
            
            // ProcessHistoryの確認
            status.HasProcessHistory = await connection.ExecuteScalarAsync<bool>(@"
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM ProcessHistory 
                    WHERE JobDate = @JobDate 
                        AND ProcessType IN ('DAILY_CLOSE', 'DAILY_REPORT')
                ) THEN 1 ELSE 0 END",
                new { JobDate = jobDate });
            
            // 商品日報の存在確認
            status.HasDailyReport = await connection.ExecuteScalarAsync<bool>(@"
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM ProcessHistory 
                    WHERE JobDate = @JobDate 
                        AND ProcessType = 'DAILY_REPORT'
                        AND Status = 'SUCCESS'
                ) THEN 1 ELSE 0 END",
                new { JobDate = jobDate });
            
            // 翌日以降のデータ確認
            status.HasNextDayData = await CheckNextDayDataAsync(connection, jobDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "関連データ状態の取得でエラーが発生しました");
        }
        
        return status;
    }
    
    private async Task<bool> CheckNextDayDataAsync(SqlConnection connection, DateTime jobDate, SqlTransaction? transaction = null)
    {
        var nextDay = jobDate.AddDays(1);
        
        var sql = @"
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM SalesVouchers WHERE JobDate >= @NextDay
                UNION ALL
                SELECT 1 FROM PurchaseVouchers WHERE JobDate >= @NextDay
                UNION ALL
                SELECT 1 FROM InventoryAdjustments WHERE JobDate >= @NextDay
            ) THEN 1 ELSE 0 END";
        
        return await connection.ExecuteScalarAsync<bool>(sql, new { NextDay = nextDay }, transaction);
    }
}