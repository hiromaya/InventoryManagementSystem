using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// データセット管理リポジトリ実装
/// </summary>
public class DataSetManagementRepository : BaseRepository, IDataSetManagementRepository
{
    public DataSetManagementRepository(string connectionString, ILogger<DataSetManagementRepository> logger)
        : base(connectionString, logger)
    {
    }
    
    /// <inheritdoc/>
    public async Task<DataSetManagement> CreateAsync(DataSetManagement dataset)
    {
        const string sql = @"
            INSERT INTO DataSetManagement (
                DatasetId, JobDate, ProcessType, ImportType, RecordCount, TotalRecordCount,
                IsActive, IsArchived, ParentDataSetId, ImportedFiles, CreatedAt, CreatedBy, 
                Notes, Department
            ) VALUES (
                @DatasetId, @JobDate, @ProcessType, @ImportType, @RecordCount, @TotalRecordCount,
                @IsActive, @IsArchived, @ParentDataSetId, @ImportedFiles, @CreatedAt, @CreatedBy, 
                @Notes, @Department
            )";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            // TotalRecordCountがセットされていない場合はRecordCountと同じ値を設定
            if (dataset.TotalRecordCount == 0 && dataset.RecordCount > 0)
            {
                dataset.TotalRecordCount = dataset.RecordCount;
            }
            
            await connection.ExecuteAsync(sql, dataset);
            
            _logger.LogInformation("データセット作成完了: DatasetId={DatasetId}, Department={Department}", 
                dataset.DatasetId, dataset.Department);
            
            return dataset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット作成エラー");
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<DataSetManagement?> GetByIdAsync(string datasetId)
    {
        const string sql = "SELECT * FROM DataSetManagement WHERE DatasetId = @DatasetId";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var dataset = await connection.QueryFirstOrDefaultAsync<DataSetManagement>(
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
    public async Task<DataSetManagement?> GetLatestByJobDateAndTypeAsync(DateTime jobDate, string processType)
    {
        const string sql = @"
            SELECT TOP 1 * FROM DataSetManagement 
            WHERE JobDate = @JobDate AND ProcessType = @ProcessType
            ORDER BY CreatedAt DESC";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var dataset = await connection.QueryFirstOrDefaultAsync<DataSetManagement>(
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
    public async Task<IEnumerable<DataSetManagement>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT * FROM DataSetManagement 
            WHERE JobDate = @JobDate
            ORDER BY CreatedAt DESC";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var datasets = await connection.QueryAsync<DataSetManagement>(
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
    public async Task<DataSetManagement?> GetActiveByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT TOP 1 *
            FROM DataSetManagement
            WHERE JobDate = @JobDate AND IsActive = 1
            ORDER BY CreatedAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<DataSetManagement>(sql, new { JobDate = jobDate });
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
            UPDATE DataSetManagement
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