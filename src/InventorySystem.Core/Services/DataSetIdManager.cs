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
    /// DataSetIdの一元管理サービス
    /// JobDateとJobTypeに基づいてDataSetIdの一意性を保証する
    /// </summary>
    public class DataSetIdManager : IDataSetIdManager
    {
        private readonly string _connectionString;
        private readonly ILogger<DataSetIdManager> _logger;

        public DataSetIdManager(
            IConfiguration configuration,
            ILogger<DataSetIdManager> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("DefaultConnection not found");
            _logger = logger;
        }

        /// <summary>
        /// 指定されたジョブ日付とジョブタイプに対応するDataSetIdを取得します。
        /// 存在しない場合は新しいIDを生成・永続化して返します。
        /// 注意: このメソッドは非推奨です。CreateNewDataSetIdAsyncを使用してください。
        /// </summary>
        [Obsolete("このメソッドは非推奨です。CreateNewDataSetIdAsyncを使用してください。")]
        public async Task<string> GetOrCreateDataSetIdAsync(DateTime jobDate, string jobType)
        {
            // 新しいメソッドにリダイレクト（後方互換性のため残す）
            _logger.LogWarning(
                "GetOrCreateDataSetIdAsyncは非推奨です。CreateNewDataSetIdAsyncを使用してください。");
            return await CreateNewDataSetIdAsync(jobDate, jobType);
        }

        /// <summary>
        /// 売上伝票のDataSetIdを取得（Process 2-5用）
        /// </summary>
        public async Task<string?> GetSalesVoucherDataSetIdAsync(DateTime jobDate)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // JobExecutionLogから売上伝票のDataSetIdを取得
                var dataSetId = await GetExistingDataSetIdAsync(connection, jobDate, "SalesVoucher");
                
                if (string.IsNullOrEmpty(dataSetId))
                {
                    // JobExecutionLogにない場合は、SalesVouchersテーブルから直接取得
                    dataSetId = await GetSalesVoucherDataSetIdFromTableAsync(connection, jobDate);
                    
                    // 見つかった場合はJobExecutionLogに記録
                    if (!string.IsNullOrEmpty(dataSetId))
                    {
                        try
                        {
                            await InsertJobExecutionLogAsync(connection, jobDate, "SalesVoucher", dataSetId);
                            _logger.LogInformation("SalesVouchersテーブルからDataSetIdを復元: JobDate={JobDate}, DataSetId={DataSetId}", 
                                jobDate.ToString("yyyy-MM-dd"), dataSetId);
                        }
                        catch (SqlException sqlEx) when (sqlEx.Number == 2627) // UNIQUE制約違反
                        {
                            // すでに存在する場合は無視
                            _logger.LogDebug("DataSetIdは既に登録済み: {DataSetId}", dataSetId);
                        }
                    }
                }

                return dataSetId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "売上伝票DataSetId取得でエラーが発生: JobDate={JobDate}", 
                    jobDate.ToString("yyyy-MM-dd"));
                return null;
            }
        }

        /// <summary>
        /// 指定されたJobDateとJobTypeのDataSetIdが存在するかチェック
        /// </summary>
        public async Task<bool> ExistsAsync(DateTime jobDate, string jobType)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var dataSetId = await GetExistingDataSetIdAsync(connection, jobDate, jobType);
                return !string.IsNullOrEmpty(dataSetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSetId存在確認でエラーが発生: JobDate={JobDate}, JobType={JobType}", 
                    jobDate.ToString("yyyy-MM-dd"), jobType);
                return false;
            }
        }

        /// <summary>
        /// JobDateで使用されているすべてのDataSetIdを取得
        /// </summary>
        public async Task<List<string>> GetAllDataSetIdsAsync(DateTime jobDate)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT DataSetId 
                    FROM JobExecutionLog 
                    WHERE JobDate = @JobDate 
                    ORDER BY CreatedAt DESC";

                var result = await connection.QueryAsync<string>(sql, new { JobDate = jobDate });
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "全DataSetId取得でエラーが発生: JobDate={JobDate}", 
                    jobDate.ToString("yyyy-MM-dd"));
                return new List<string>();
            }
        }

        /// <summary>
        /// 既存のDataSetIdを取得
        /// </summary>
        private async Task<string?> GetExistingDataSetIdAsync(
            IDbConnection connection, DateTime jobDate, string jobType)
        {
            const string sql = @"
                SELECT DataSetId 
                FROM JobExecutionLog 
                WHERE JobDate = @JobDate AND JobType = @JobType";

            return await connection.QuerySingleOrDefaultAsync<string>(
                sql, new { JobDate = jobDate, JobType = jobType });
        }

        /// <summary>
        /// SalesVouchersテーブルから直接DataSetIdを取得
        /// </summary>
        private async Task<string?> GetSalesVoucherDataSetIdFromTableAsync(
            IDbConnection connection, DateTime jobDate)
        {
            const string sql = @"
                SELECT DISTINCT TOP 1 DataSetId 
                FROM SalesVouchers 
                WHERE JobDate = @JobDate 
                  AND DataSetId IS NOT NULL 
                ORDER BY CreatedDate DESC";

            return await connection.QuerySingleOrDefaultAsync<string>(
                sql, new { JobDate = jobDate });
        }

        /// <summary>
        /// JobExecutionLogにレコードを挿入
        /// </summary>
        private async Task InsertJobExecutionLogAsync(
            IDbConnection connection, DateTime jobDate, string jobType, string dataSetId)
        {
            const string sql = @"
                INSERT INTO JobExecutionLog (JobDate, JobType, DataSetId, CreatedAt, CreatedBy)
                VALUES (@JobDate, @JobType, @DataSetId, GETDATE(), @CreatedBy)";

            await connection.ExecuteAsync(sql, new 
            { 
                JobDate = jobDate, 
                JobType = jobType, 
                DataSetId = dataSetId,
                CreatedBy = Environment.UserName ?? "System"
            });
        }

        /// <summary>
        /// 新しいDataSetIdを生成し、古いDataSetを無効化する
        /// </summary>
        public async Task<string> CreateNewDataSetIdAsync(DateTime jobDate, string jobType)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                // 常に新しいDataSetIdを生成
                var newDataSetId = Guid.NewGuid().ToString();
                
                // JobExecutionLogの既存レコードを削除（UNIQUE制約回避）
                await DeleteExistingJobExecutionLogAsync(connection, jobDate, jobType);
                
                // 新しいレコードを挿入
                await InsertJobExecutionLogAsync(connection, jobDate, jobType, newDataSetId);
                
                _logger.LogInformation(
                    "新しいDataSetIdを生成（古いDataSetは無効化対象）: " +
                    "JobDate={JobDate}, JobType={JobType}, DataSetId={DataSetId}", 
                    jobDate.ToString("yyyy-MM-dd"), jobType, newDataSetId);
                
                return newDataSetId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "DataSetId生成でエラーが発生: JobDate={JobDate}, JobType={JobType}", 
                    jobDate.ToString("yyyy-MM-dd"), jobType);
                throw;
            }
        }

        /// <summary>
        /// 既存のJobExecutionLogレコードを削除
        /// </summary>
        private async Task DeleteExistingJobExecutionLogAsync(
            IDbConnection connection, DateTime jobDate, string jobType)
        {
            const string sql = @"
                DELETE FROM JobExecutionLog 
                WHERE JobDate = @JobDate AND JobType = @JobType";
            
            var deleted = await connection.ExecuteAsync(sql, new { JobDate = jobDate, JobType = jobType });
            
            if (deleted > 0)
            {
                _logger.LogInformation(
                    "既存のJobExecutionLogレコードを削除: JobDate={JobDate}, JobType={JobType}", 
                    jobDate, jobType);
            }
        }
    }
}