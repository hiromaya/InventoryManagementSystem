using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// 処理履歴リポジトリ実装
/// </summary>
public class ProcessHistoryRepository : BaseRepository, IProcessHistoryRepository
{
    public ProcessHistoryRepository(string connectionString, ILogger<ProcessHistoryRepository> logger)
        : base(connectionString, logger)
    {
    }
    
    /// <inheritdoc/>
    public async Task<ProcessHistory> CreateAsync(ProcessHistory history)
    {
        const string sql = @"
            INSERT INTO ProcessHistory (
                DatasetId, JobDate, ProcessType, StartTime, Status, ExecutedBy
            ) 
            OUTPUT INSERTED.*
            VALUES (
                @DatasetId, @JobDate, @ProcessType, @StartTime, @Status, @ExecutedBy
            )";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var created = await connection.QuerySingleAsync<ProcessHistory>(sql, history);
            
            _logger.LogInformation("処理履歴作成完了: Id={Id}, DatasetId={DatasetId}", 
                created.Id, created.DataSetId);
            
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理履歴作成エラー");
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task UpdateAsync(ProcessHistory history)
    {
        const string sql = @"
            UPDATE ProcessHistory 
            SET EndTime = @EndTime,
                Status = @Status,
                ErrorMessage = @ErrorMessage
            WHERE Id = @Id";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, history);
            
            _logger.LogInformation("処理履歴更新完了: Id={Id}", history.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理履歴更新エラー: Id={Id}", history.Id);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<ProcessHistory>> GetByJobDateAndTypeAsync(DateTime jobDate, string processType)
    {
        const string sql = @"
            SELECT * FROM ProcessHistory 
            WHERE JobDate = @JobDate AND ProcessType = @ProcessType
            ORDER BY StartTime DESC";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var histories = await connection.QueryAsync<ProcessHistory>(sql, new { JobDate = jobDate.Date, ProcessType = processType });
            
            return histories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理履歴取得エラー: JobDate={JobDate}, ProcessType={ProcessType}", 
                jobDate, processType);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<ProcessHistory?> GetLastSuccessfulAsync(string processType)
    {
        const string sql = @"
            SELECT TOP 1 * FROM ProcessHistory 
            WHERE ProcessType = @ProcessType AND Status = @Status
            ORDER BY StartTime DESC";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var history = await connection.QueryFirstOrDefaultAsync<ProcessHistory>(
                sql, 
                new { ProcessType = processType, Status = (int)ProcessStatus.Completed });
            
            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "最終成功処理履歴取得エラー: ProcessType={ProcessType}", processType);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<ProcessHistory?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM ProcessHistory WHERE Id = @Id";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var history = await connection.QueryFirstOrDefaultAsync<ProcessHistory>(sql, new { Id = id });
            
            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理履歴取得エラー: Id={Id}", id);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<ProcessHistory?> GetByDatasetIdAsync(string datasetId)
    {
        const string sql = "SELECT * FROM ProcessHistory WHERE DatasetId = @DatasetId";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var history = await connection.QueryFirstOrDefaultAsync<ProcessHistory>(sql, new { DatasetId = datasetId });
            
            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理履歴取得エラー: DatasetId={DatasetId}", datasetId);
            throw;
        }
    }
}