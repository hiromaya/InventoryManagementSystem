using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Exceptions;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// 日次終了管理リポジトリ実装
/// </summary>
public class DailyCloseManagementRepository : BaseRepository, IDailyCloseManagementRepository
{
    public DailyCloseManagementRepository(string connectionString, ILogger<DailyCloseManagementRepository> logger)
        : base(connectionString, logger)
    {
    }
    
    /// <inheritdoc/>
    public async Task<DailyCloseManagement> CreateAsync(DailyCloseManagement dailyClose)
    {
        const string sql = @"
            INSERT INTO DailyCloseManagement (
                JobDate, DataSetId, DailyReportDataSetId, BackupPath, 
                ProcessedAt, ProcessedBy, DataHash, ValidationStatus, Remarks
            ) 
            OUTPUT INSERTED.*
            VALUES (
                @JobDate, @DataSetId, @DailyReportDataSetId, @BackupPath, 
                @ProcessedAt, @ProcessedBy, @DataHash, @ValidationStatus, @Remarks
            )";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            // デフォルト値の設定（Gemini推奨：テーブルのデフォルト値に頼らず明示的に設定）
            if (dailyClose.ProcessedAt == default)
            {
                dailyClose.ProcessedAt = DateTime.Now;
            }
            
            if (string.IsNullOrEmpty(dailyClose.ValidationStatus))
            {
                dailyClose.ValidationStatus = "PENDING";
            }
            
            var created = await connection.QuerySingleAsync<DailyCloseManagement>(sql, dailyClose);
            
            _logger.LogInformation(
                "日次終了管理作成完了: Id={Id}, JobDate={JobDate}, DataSetId={DataSetId}, ValidationStatus={Status}", 
                created.Id, created.JobDate, created.DataSetId, created.ValidationStatus);
            
            return created;
        }
        catch (SqlException ex) when (ex.Number == 2627) // ユニーク制約違反
        {
            _logger.LogWarning(ex, "日次終了管理の重複作成試行: JobDate={JobDate}", dailyClose.JobDate);
            // 業務例外に変換してスロー（Gemini推奨）
            throw new DuplicateDailyCloseException(dailyClose.JobDate, dailyClose.ValidationStatus, null, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "日次終了管理作成エラー");
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<DailyCloseManagement?> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = "SELECT * FROM DailyCloseManagement WHERE JobDate = @JobDate";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var dailyClose = await connection.QueryFirstOrDefaultAsync<DailyCloseManagement>(
                sql, new { JobDate = jobDate.Date });
            
            return dailyClose;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "日次終了管理取得エラー: JobDate={JobDate}", jobDate);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<DailyCloseManagement?> GetLatestAsync()
    {
        const string sql = @"
            SELECT TOP 1 * FROM DailyCloseManagement 
            ORDER BY JobDate DESC";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var dailyClose = await connection.QueryFirstOrDefaultAsync<DailyCloseManagement>(sql);
            
            return dailyClose;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "最新日次終了管理取得エラー");
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task UpdateStatusAsync(int id, string status, string? remark = null)
    {
        const string sql = @"
            UPDATE DailyCloseManagement 
            SET 
                ValidationStatus = @Status,
                Remarks = CASE 
                    WHEN @Remark IS NOT NULL AND Remarks IS NOT NULL 
                    THEN '[' + FORMAT(GETDATE(), 'yyyy-MM-dd HH:mm:ss') + '] ' + @Remark + CHAR(13) + CHAR(10) + Remarks
                    WHEN @Remark IS NOT NULL AND Remarks IS NULL 
                    THEN '[' + FORMAT(GETDATE(), 'yyyy-MM-dd HH:mm:ss') + '] ' + @Remark
                    ELSE Remarks
                END
            WHERE Id = @Id";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id, Status = status, Remark = remark });
            
            if (affectedRows == 0)
            {
                _logger.LogWarning("日次終了管理が見つかりません: Id={Id}", id);
                throw new InvalidOperationException($"ID {id} の日次終了管理が見つかりません。");
            }
            
            _logger.LogInformation("日次終了管理ステータス更新完了: Id={Id}, Status={Status}, Remark={Remark}", 
                id, status, remark ?? "なし");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "日次終了管理ステータス更新エラー: Id={Id}", id);
            throw;
        }
    }
}