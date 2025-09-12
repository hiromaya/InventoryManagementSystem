using Dapper;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces.Masters;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories.Masters;

/// <summary>
/// 産地マスタリポジトリ実装
/// </summary>
public class RegionMasterRepository : IRegionMasterRepository
{
    private readonly string _connectionString;
    private readonly ILogger<RegionMasterRepository> _logger;

    public RegionMasterRepository(string connectionString, ILogger<RegionMasterRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// 産地マスタを取得
    /// </summary>
    public async Task<RegionMaster?> GetByCodeAsync(string regionCode)
    {
        const string sql = @"
            SELECT 
                RegionCode,
                RegionName,
                SearchKana,
                NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
                DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
                TextValue1, TextValue2, TextValue3, TextValue4, TextValue5
            FROM RegionMaster
            WHERE RegionCode = @RegionCode";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<RegionMaster>(sql, new { RegionCode = regionCode });
    }

    /// <summary>
    /// 全ての産地マスタを取得
    /// </summary>
    public async Task<IEnumerable<RegionMaster>> GetAllAsync()
    {
        const string sql = @"
            SELECT 
                RegionCode,
                RegionName,
                SearchKana,
                NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
                DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
                TextValue1, TextValue2, TextValue3, TextValue4, TextValue5
            FROM RegionMaster
            ORDER BY RegionCode";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<RegionMaster>(sql);
    }

    /// <summary>
    /// 産地マスタを登録または更新
    /// </summary>
    public async Task<bool> UpsertAsync(RegionMaster region)
    {
        const string sql = @"
            MERGE RegionMaster AS target
            USING (SELECT @RegionCode AS RegionCode) AS source
            ON target.RegionCode = source.RegionCode
            WHEN MATCHED THEN
                UPDATE SET 
                    RegionName = @RegionName,
                    SearchKana = @SearchKana,
                    NumericValue1 = @NumericValue1, NumericValue2 = @NumericValue2, 
                    NumericValue3 = @NumericValue3, NumericValue4 = @NumericValue4, 
                    NumericValue5 = @NumericValue5,
                    DateValue1 = @DateValue1, DateValue2 = @DateValue2, 
                    DateValue3 = @DateValue3, DateValue4 = @DateValue4, 
                    DateValue5 = @DateValue5,
                    TextValue1 = @TextValue1, TextValue2 = @TextValue2, 
                    TextValue3 = @TextValue3, TextValue4 = @TextValue4, 
                    TextValue5 = @TextValue5
            WHEN NOT MATCHED THEN
                INSERT (
                    RegionCode, RegionName, SearchKana,
                    NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
                    DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
                    TextValue1, TextValue2, TextValue3, TextValue4, TextValue5
                ) VALUES (
                    @RegionCode, @RegionName, @SearchKana,
                    @NumericValue1, @NumericValue2, @NumericValue3, @NumericValue4, @NumericValue5,
                    @DateValue1, @DateValue2, @DateValue3, @DateValue4, @DateValue5,
                    @TextValue1, @TextValue2, @TextValue3, @TextValue4, @TextValue5
                );";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.ExecuteAsync(sql, region);
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "産地マスタの登録/更新でエラーが発生しました。産地コード: {Code}", region.RegionCode);
            throw;
        }
    }

    /// <summary>
    /// 産地マスタを一括登録または更新
    /// </summary>
    public async Task<int> BulkUpsertAsync(IEnumerable<RegionMaster> regions)
    {
        const string sql = @"
            MERGE RegionMaster AS target
            USING (SELECT @RegionCode AS RegionCode) AS source
            ON target.RegionCode = source.RegionCode
            WHEN MATCHED THEN
                UPDATE SET 
                    RegionName = @RegionName,
                    SearchKana = @SearchKana,
                    NumericValue1 = @NumericValue1, NumericValue2 = @NumericValue2, 
                    NumericValue3 = @NumericValue3, NumericValue4 = @NumericValue4, 
                    NumericValue5 = @NumericValue5,
                    DateValue1 = @DateValue1, DateValue2 = @DateValue2, 
                    DateValue3 = @DateValue3, DateValue4 = @DateValue4, 
                    DateValue5 = @DateValue5,
                    TextValue1 = @TextValue1, TextValue2 = @TextValue2, 
                    TextValue3 = @TextValue3, TextValue4 = @TextValue4, 
                    TextValue5 = @TextValue5
            WHEN NOT MATCHED THEN
                INSERT (
                    RegionCode, RegionName, SearchKana,
                    NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
                    DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
                    TextValue1, TextValue2, TextValue3, TextValue4, TextValue5
                ) VALUES (
                    @RegionCode, @RegionName, @SearchKana,
                    @NumericValue1, @NumericValue2, @NumericValue3, @NumericValue4, @NumericValue5,
                    @DateValue1, @DateValue2, @DateValue3, @DateValue4, @DateValue5,
                    @TextValue1, @TextValue2, @TextValue3, @TextValue4, @TextValue5
                );";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            var count = 0;
            foreach (var region in regions)
            {
                var result = await connection.ExecuteAsync(sql, region, transaction);
                if (result > 0) count++;
            }
            
            transaction.Commit();
            _logger.LogInformation("産地マスタを{Count}件一括登録/更新しました", count);
            return count;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "産地マスタの一括登録/更新でエラーが発生しました");
            throw;
        }
    }

    /// <summary>
    /// 産地マスタを削除
    /// </summary>
    public async Task<bool> DeleteAsync(string regionCode)
    {
        const string sql = "DELETE FROM RegionMaster WHERE RegionCode = @RegionCode";

        using var connection = new SqlConnection(_connectionString);
        var result = await connection.ExecuteAsync(sql, new { RegionCode = regionCode });
        return result > 0;
    }

    /// <summary>
    /// 産地コードの存在確認
    /// </summary>
    public async Task<bool> ExistsAsync(string regionCode)
    {
        const string sql = "SELECT COUNT(1) FROM RegionMaster WHERE RegionCode = @RegionCode";

        using var connection = new SqlConnection(_connectionString);
        var count = await connection.ExecuteScalarAsync<int>(sql, new { RegionCode = regionCode });
        return count > 0;
    }

    /// <summary>
    /// 産地マスタ数を取得
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        const string sql = "SELECT COUNT(*) FROM RegionMaster";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql);
    }
}