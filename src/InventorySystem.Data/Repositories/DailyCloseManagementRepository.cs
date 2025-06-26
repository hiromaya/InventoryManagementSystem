using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

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
                JobDate, DatasetId, DailyReportDatasetId, BackupPath, ProcessedAt, ProcessedBy
            ) 
            OUTPUT INSERTED.*
            VALUES (
                @JobDate, @DatasetId, @DailyReportDatasetId, @BackupPath, @ProcessedAt, @ProcessedBy
            )";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var created = await connection.QuerySingleAsync<DailyCloseManagement>(sql, dailyClose);
            
            _logger.LogInformation("日次終了管理作成完了: Id={Id}, JobDate={JobDate}", 
                created.Id, created.JobDate);
            
            return created;
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
}