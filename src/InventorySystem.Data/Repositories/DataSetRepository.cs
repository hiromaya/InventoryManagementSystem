using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// データセット管理リポジトリ実装
/// </summary>
public class DataSetRepository : BaseRepository, IDataSetRepository
{
    public DataSetRepository(string connectionString, ILogger<DataSetRepository> logger)
        : base(connectionString, logger)
    {
    }

    /// <summary>
    /// データセットを作成
    /// </summary>
    public async Task<string> CreateAsync(DataSet dataSet)
    {
        const string sql = @"
            INSERT INTO DataSets (
                Id, DataSetType, ImportedAt, Status, JobDate,
                RecordCount, ErrorMessage, FilePath, CreatedAt, UpdatedAt
            ) VALUES (
                @Id, @DataSetType, @ImportedAt, @Status, @JobDate,
                @RecordCount, @ErrorMessage, @FilePath, @CreatedAt, @UpdatedAt
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var parameters = new
            {
                dataSet.Id,
                dataSet.DataSetType, // DataSetTypeとして登録
                ImportedAt = dataSet.ImportedAt,
                dataSet.Status,
                dataSet.JobDate,
                dataSet.RecordCount,
                dataSet.ErrorMessage,
                dataSet.FilePath,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("データセット作成完了: {DataSetId}, Type: {DataSetType}", 
                dataSet.Id, dataSet.DataSetType);
            
            return dataSet.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット作成エラー: {DataSetId}", dataSet.Id);
            throw;
        }
    }

    /// <summary>
    /// データセットを取得
    /// </summary>
    public async Task<DataSet?> GetByIdAsync(string id)
    {
        const string sql = @"
            SELECT Id, DataSetType, ImportedAt, RecordCount, Status, 
                   ErrorMessage, FilePath, JobDate, CreatedAt, UpdatedAt
            FROM DataSets 
            WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var dataSet = await connection.QueryFirstOrDefaultAsync<DataSet>(sql, new { Id = id });
            
            return dataSet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット取得エラー: {DataSetId}", id);
            throw;
        }
    }

    /// <summary>
    /// データセットのステータスを更新
    /// </summary>
    public async Task UpdateStatusAsync(string id, string status, string? errorMessage = null)
    {
        const string sql = @"
            UPDATE DataSets 
            SET Status = @Status, 
                ErrorMessage = @ErrorMessage,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var parameters = new
            {
                Id = id,
                Status = status,
                ErrorMessage = errorMessage,
                UpdatedAt = DateTime.Now
            };

            var affectedRows = await connection.ExecuteAsync(sql, parameters);
            
            if (affectedRows == 0)
            {
                throw new InvalidOperationException($"データセットが見つかりません: {id}");
            }

            _logger.LogInformation("データセットステータス更新: {DataSetId} -> {Status}", id, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセットステータス更新エラー: {DataSetId}", id);
            throw;
        }
    }

    /// <summary>
    /// データセットの件数を更新
    /// </summary>
    public async Task UpdateRecordCountAsync(string id, int recordCount)
    {
        const string sql = @"
            UPDATE DataSets 
            SET RecordCount = @RecordCount,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var parameters = new
            {
                Id = id,
                RecordCount = recordCount,
                UpdatedAt = DateTime.Now
            };

            await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("データセット件数更新: {DataSetId} -> {RecordCount}件", id, recordCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット件数更新エラー: {DataSetId}", id);
            throw;
        }
    }

    /// <summary>
    /// 指定した日付のデータセット一覧を取得
    /// </summary>
    public async Task<IEnumerable<DataSet>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT Id, DataSetType, ImportedAt, RecordCount, Status, 
                   ErrorMessage, FilePath, JobDate, CreatedAt, UpdatedAt
            FROM DataSets 
            WHERE JobDate = @JobDate
            ORDER BY ImportedAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var dataSets = await connection.QueryAsync<DataSet>(sql, new { JobDate = jobDate.Date });
            
            return dataSets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット一覧取得エラー: {JobDate}", jobDate);
            throw;
        }
    }

    /// <summary>
    /// 指定したステータスのデータセット一覧を取得
    /// </summary>
    public async Task<IEnumerable<DataSet>> GetByStatusAsync(string status)
    {
        const string sql = @"
            SELECT Id, DataSetType, ImportedAt, RecordCount, Status, 
                   ErrorMessage, FilePath, JobDate, CreatedAt, UpdatedAt
            FROM DataSets 
            WHERE Status = @Status
            ORDER BY ImportedAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var dataSets = await connection.QueryAsync<DataSet>(sql, new { Status = status });
            
            return dataSets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ステータス別データセット取得エラー: {Status}", status);
            throw;
        }
    }

    /// <summary>
    /// データセットを削除
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        const string sql = "DELETE FROM DataSets WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
            
            if (affectedRows == 0)
            {
                throw new InvalidOperationException($"データセットが見つかりません: {id}");
            }

            _logger.LogInformation("データセット削除完了: {DataSetId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット削除エラー: {DataSetId}", id);
            throw;
        }
    }

    /// <summary>
    /// 処理完了したデータセットの件数を取得
    /// </summary>
    public async Task<int> GetCompletedCountAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM DataSets 
            WHERE JobDate = @JobDate AND Status = @Status";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var count = await connection.QuerySingleAsync<int>(sql, 
                new { JobDate = jobDate.Date, Status = InventorySystem.Core.Entities.DataSetStatus.Completed });
            
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理完了データセット件数取得エラー: {JobDate}", jobDate);
            throw;
        }
    }
}