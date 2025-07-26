using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// アンマッチチェック結果リポジトリ実装
/// アンマッチチェック0件必須機能のためのデータアクセス実装
/// </summary>
public class UnmatchCheckRepository : BaseRepository, IUnmatchCheckRepository
{
    public UnmatchCheckRepository(string connectionString, ILogger<UnmatchCheckRepository> logger)
        : base(connectionString, logger)
    {
    }

    /// <summary>
    /// アンマッチチェック結果を保存または更新（Upsert）
    /// </summary>
    public async Task<bool> SaveOrUpdateAsync(UnmatchCheckResult result)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                MERGE UnmatchCheckResult AS target
                USING (SELECT @DataSetId AS DataSetId) AS source
                ON target.DataSetId = source.DataSetId
                WHEN MATCHED THEN
                    UPDATE SET
                        CheckDateTime = @CheckDateTime,
                        UnmatchCount = @UnmatchCount,
                        HasFullWidthError = @HasFullWidthError,
                        IsPassed = @IsPassed,
                        CheckStatus = @CheckStatus,
                        ErrorMessage = @ErrorMessage,
                        UpdatedAt = @UpdatedAt
                WHEN NOT MATCHED THEN
                    INSERT (DataSetId, CheckDateTime, UnmatchCount, HasFullWidthError, IsPassed, 
                           CheckStatus, ErrorMessage, CreatedAt, UpdatedAt)
                    VALUES (@DataSetId, @CheckDateTime, @UnmatchCount, @HasFullWidthError, @IsPassed,
                           @CheckStatus, @ErrorMessage, @CreatedAt, @UpdatedAt);
                """;

            var parameters = new
            {
                result.DataSetId,
                result.CheckDateTime,
                result.UnmatchCount,
                result.HasFullWidthError,
                result.IsPassed,
                result.CheckStatus,
                result.ErrorMessage,
                result.CreatedAt,
                UpdatedAt = DateTime.Now
            };

            var rowsAffected = await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("アンマッチチェック結果を保存しました - DataSetId: {DataSetId}, Status: {Status}, Count: {Count}",
                result.DataSetId, result.CheckStatus, result.UnmatchCount);

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "アンマッチチェック結果の保存に失敗しました - DataSetId: {DataSetId}", result.DataSetId);
            return false;
        }
    }

    /// <summary>
    /// 指定されたDataSetIdの最新アンマッチチェック結果を取得
    /// </summary>
    public async Task<UnmatchCheckResult?> GetByDataSetIdAsync(string dataSetId)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT DataSetId, CheckDateTime, UnmatchCount, HasFullWidthError, IsPassed,
                       CheckStatus, ErrorMessage, CreatedAt, UpdatedAt
                FROM UnmatchCheckResult
                WHERE DataSetId = @DataSetId
                """;

            var result = await connection.QueryFirstOrDefaultAsync<UnmatchCheckResult>(sql, new { DataSetId = dataSetId });
            
            if (result != null)
            {
                LogDebug("アンマッチチェック結果を取得しました", new { DataSetId = dataSetId, Status = result.CheckStatus });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "アンマッチチェック結果の取得に失敗しました - DataSetId: {DataSetId}", dataSetId);
            return null;
        }
    }

    /// <summary>
    /// 最新のアンマッチチェック結果を取得
    /// </summary>
    public async Task<UnmatchCheckResult?> GetLatestAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT TOP 1 DataSetId, CheckDateTime, UnmatchCount, HasFullWidthError, IsPassed,
                             CheckStatus, ErrorMessage, CreatedAt, UpdatedAt
                FROM UnmatchCheckResult
                ORDER BY CheckDateTime DESC
                """;

            var result = await connection.QueryFirstOrDefaultAsync<UnmatchCheckResult>(sql);
            
            if (result != null)
            {
                LogDebug("最新のアンマッチチェック結果を取得しました", new { DataSetId = result.DataSetId, CheckDateTime = result.CheckDateTime });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "最新のアンマッチチェック結果の取得に失敗しました");
            return null;
        }
    }

    /// <summary>
    /// 合格済み（IsPassed=true）の結果一覧を取得
    /// </summary>
    public async Task<IEnumerable<UnmatchCheckResult>> GetPassedResultsAsync(int limit = 10)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT TOP (@Limit) DataSetId, CheckDateTime, UnmatchCount, HasFullWidthError, IsPassed,
                                   CheckStatus, ErrorMessage, CreatedAt, UpdatedAt
                FROM UnmatchCheckResult
                WHERE IsPassed = 1
                ORDER BY CheckDateTime DESC
                """;

            var results = await connection.QueryAsync<UnmatchCheckResult>(sql, new { Limit = limit });
            
            LogDebug("合格済みアンマッチチェック結果を取得しました", new { Count = results.Count() });
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "合格済みアンマッチチェック結果の取得に失敗しました");
            return Enumerable.Empty<UnmatchCheckResult>();
        }
    }

    /// <summary>
    /// 指定期間内のアンマッチチェック結果を取得
    /// </summary>
    public async Task<IEnumerable<UnmatchCheckResult>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT DataSetId, CheckDateTime, UnmatchCount, HasFullWidthError, IsPassed,
                       CheckStatus, ErrorMessage, CreatedAt, UpdatedAt
                FROM UnmatchCheckResult
                WHERE CheckDateTime >= @StartDate AND CheckDateTime <= @EndDate
                ORDER BY CheckDateTime DESC
                """;

            var results = await connection.QueryAsync<UnmatchCheckResult>(sql, new { StartDate = startDate, EndDate = endDate });
            
            LogDebug("期間内アンマッチチェック結果を取得しました", new { StartDate = startDate, EndDate = endDate, Count = results.Count() });
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "期間内アンマッチチェック結果の取得に失敗しました - 期間: {StartDate} - {EndDate}",
                startDate, endDate);
            return Enumerable.Empty<UnmatchCheckResult>();
        }
    }

    /// <summary>
    /// 古いアンマッチチェック結果を削除
    /// </summary>
    public async Task<int> CleanupOldResultsAsync(int keepDays = 30)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var cutoffDate = DateTime.Now.AddDays(-keepDays);

            const string sql = """
                DELETE FROM UnmatchCheckResult
                WHERE CreatedAt < @CutoffDate
                """;

            var deletedCount = await connection.ExecuteAsync(sql, new { CutoffDate = cutoffDate });
            
            if (deletedCount > 0)
            {
                _logger.LogInformation("古いアンマッチチェック結果を削除しました - 削除件数: {Count}, 保持日数: {KeepDays}日",
                    deletedCount, keepDays);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "古いアンマッチチェック結果の削除に失敗しました - 保持日数: {KeepDays}日", keepDays);
            return 0;
        }
    }

    /// <summary>
    /// アンマッチチェック結果の統計情報を取得
    /// </summary>
    public async Task<(int PassedCount, int FailedCount, int ErrorCount)> GetStatisticsAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT 
                    SUM(CASE WHEN CheckStatus = 'Passed' THEN 1 ELSE 0 END) as PassedCount,
                    SUM(CASE WHEN CheckStatus = 'Failed' THEN 1 ELSE 0 END) as FailedCount,
                    SUM(CASE WHEN CheckStatus = 'Error' THEN 1 ELSE 0 END) as ErrorCount
                FROM UnmatchCheckResult
                """;

            var result = await connection.QueryFirstOrDefaultAsync(sql);
            
            var passedCount = result?.PassedCount ?? 0;
            var failedCount = result?.FailedCount ?? 0;
            var errorCount = result?.ErrorCount ?? 0;

            LogDebug("アンマッチチェック統計情報を取得しました", new { PassedCount = passedCount, FailedCount = failedCount, ErrorCount = errorCount });

            return (passedCount, failedCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "アンマッチチェック統計情報の取得に失敗しました");
            return (0, 0, 0);
        }
    }
}