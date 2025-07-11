using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// データセット管理リポジトリ実装
/// </summary>
public class DatasetManagementRepository : BaseRepository, IDatasetManagementRepository
{
    public DatasetManagementRepository(string connectionString, ILogger<DatasetManagementRepository> logger)
        : base(connectionString, logger)
    {
    }
    
    /// <inheritdoc/>
    public async Task<DatasetManagement> CreateAsync(DatasetManagement dataset)
    {
        const string sql = @"
            INSERT INTO DatasetManagement (
                DatasetId, JobDate, ProcessType, ImportType, RecordCount, IsActive, IsArchived,
                ParentDataSetId, ImportedFiles, CreatedAt, CreatedBy, Notes
            ) VALUES (
                @DatasetId, @JobDate, @ProcessType, @ImportType, @RecordCount, @IsActive, @IsArchived,
                @ParentDataSetId, @ImportedFiles, @CreatedAt, @CreatedBy, @Notes
            )";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, dataset);
            
            _logger.LogInformation("データセット作成完了: DatasetId={DatasetId}", dataset.DatasetId);
            
            return dataset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット作成エラー");
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<DatasetManagement?> GetByIdAsync(string datasetId)
    {
        const string sql = "SELECT * FROM DatasetManagement WHERE DatasetId = @DatasetId";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var dataset = await connection.QueryFirstOrDefaultAsync<DatasetManagement>(
                sql, new { DatasetId = datasetId });
            
            return dataset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット取得エラー: DatasetId={DatasetId}", datasetId);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<DatasetManagement?> GetLatestByJobDateAndTypeAsync(DateTime jobDate, string processType)
    {
        const string sql = @"
            SELECT TOP 1 * FROM DatasetManagement 
            WHERE JobDate = @JobDate AND ProcessType = @ProcessType
            ORDER BY CreatedAt DESC";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var dataset = await connection.QueryFirstOrDefaultAsync<DatasetManagement>(
                sql, new { JobDate = jobDate.Date, ProcessType = processType });
            
            return dataset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "最新データセット取得エラー: JobDate={JobDate}, ProcessType={ProcessType}", 
                jobDate, processType);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<DatasetManagement>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT * FROM DatasetManagement 
            WHERE JobDate = @JobDate
            ORDER BY CreatedAt DESC";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var datasets = await connection.QueryAsync<DatasetManagement>(
                sql, new { JobDate = jobDate.Date });
            
            return datasets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット一覧取得エラー: JobDate={JobDate}", jobDate);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<DatasetManagement?> GetActiveByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT TOP 1 *
            FROM DatasetManagement
            WHERE JobDate = @JobDate AND IsActive = 1
            ORDER BY CreatedAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<DatasetManagement>(sql, new { JobDate = jobDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "アクティブデータセット取得エラー: JobDate={JobDate}", jobDate);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<int> DeactivateDataSetAsync(string dataSetId, string? deactivatedBy = null)
    {
        const string sql = @"
            UPDATE DatasetManagement
            SET IsActive = 0, 
                DeactivatedAt = GETDATE(),
                DeactivatedBy = @DeactivatedBy
            WHERE DatasetId = @DataSetId";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, DeactivatedBy = deactivatedBy });
            
            _logger.LogInformation("データセット無効化完了: DatasetId={DatasetId}", dataSetId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット無効化エラー: DatasetId={DatasetId}", dataSetId);
            throw;
        }
    }
}