using System.Data;
using Dapper;
using InventorySystem.Core.Entities;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// データセット管理リポジトリ
/// </summary>
public class DataSetManagementRepository : BaseRepository
{
    public DataSetManagementRepository(string connectionString, ILogger<DataSetManagementRepository> logger)
        : base(connectionString, logger)
    {
    }

    /// <summary>
    /// データセット情報を登録
    /// </summary>
    public async Task<int> RegisterDataSetAsync(DataSetManagement dataSet)
    {
        const string sql = @"
            INSERT INTO DataSetManagement (
                DataSetId, JobDate, ImportType, RecordCount, IsActive, IsArchived,
                ParentDataSetId, CreatedAt, CreatedBy, Notes
            ) VALUES (
                @DataSetId, @JobDate, @ImportType, @RecordCount, @IsActive, @IsArchived,
                @ParentDataSetId, @CreatedAt, @CreatedBy, @Notes
            )";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, dataSet);
            
            LogInfo($"Registered DataSet: {dataSet.DataSetId}", new { dataSet.JobDate, dataSet.ImportType });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(RegisterDataSetAsync), dataSet);
            throw;
        }
    }

    /// <summary>
    /// JobDateで有効なデータセットを取得
    /// </summary>
    public async Task<DataSetManagement?> GetActiveByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT TOP 1 *
            FROM DataSetManagement
            WHERE JobDate = @JobDate AND IsActive = 1
            ORDER BY CreatedAt DESC";

        try
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<DataSetManagement>(sql, new { JobDate = jobDate });
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetActiveByJobDateAsync), new { jobDate });
            throw;
        }
    }

    /// <summary>
    /// データセットを無効化
    /// </summary>
    public async Task<int> DeactivateDataSetAsync(string dataSetId, string? deactivatedBy = null)
    {
        const string sql = @"
            UPDATE DataSetManagement
            SET IsActive = 0, 
                DeactivatedAt = GETDATE(),
                DeactivatedBy = @DeactivatedBy
            WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, DeactivatedBy = deactivatedBy });
            
            LogInfo($"Deactivated DataSet: {dataSetId}");
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeactivateDataSetAsync), new { dataSetId });
            throw;
        }
    }

    /// <summary>
    /// 循環参照チェック
    /// </summary>
    public async Task<bool> CheckCircularReferenceAsync(string newDataSetId, string proposedParentId)
    {
        try
        {
            using var connection = CreateConnection();
            var hasCircularReference = await connection.QuerySingleAsync<bool>(
                "sp_CheckDataSetCircularReference",
                new { NewDataSetId = newDataSetId, ProposedParentId = proposedParentId },
                commandType: CommandType.StoredProcedure
            );
            
            return hasCircularReference;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(CheckCircularReferenceAsync), new { newDataSetId, proposedParentId });
            throw;
        }
    }

    /// <summary>
    /// JobDateで有効な全データセットを取得
    /// </summary>
    public async Task<List<DataSetManagement>> GetAllActiveByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT *
            FROM DataSetManagement
            WHERE JobDate = @JobDate AND IsActive = 1
            ORDER BY CreatedAt DESC";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<DataSetManagement>(sql, new { JobDate = jobDate });
            return result.ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetAllActiveByJobDateAsync), new { jobDate });
            throw;
        }
    }

    /// <summary>
    /// アーカイブ対象のデータセットを取得
    /// </summary>
    public async Task<List<DataSetManagement>> GetDataSetsForArchiveAsync(int monthsOld)
    {
        const string sql = @"
            SELECT *
            FROM DataSetManagement
            WHERE IsActive = 0 
                AND IsArchived = 0
                AND CreatedAt < DATEADD(MONTH, -@MonthsOld, GETDATE())
            ORDER BY CreatedAt";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<DataSetManagement>(sql, new { MonthsOld = monthsOld });
            return result.ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetDataSetsForArchiveAsync), new { monthsOld });
            throw;
        }
    }

    /// <summary>
    /// データセットをアーカイブ
    /// </summary>
    public async Task<int> ArchiveDataSetAsync(string dataSetId, string? archivedBy = null)
    {
        const string sql = @"
            UPDATE DataSetManagement
            SET IsArchived = 1, 
                ArchivedAt = GETDATE(),
                ArchivedBy = @ArchivedBy
            WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, ArchivedBy = archivedBy });
            
            LogInfo($"Archived DataSet: {dataSetId}");
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(ArchiveDataSetAsync), new { dataSetId });
            throw;
        }
    }
}