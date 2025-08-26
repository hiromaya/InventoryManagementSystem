using Dapper;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces.Masters;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories.Masters;

/// <summary>
/// 荷印マスタリポジトリ実装
/// </summary>
public class ShippingMarkMasterRepository : IShippingMarkMasterRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ShippingMarkMasterRepository> _logger;

    public ShippingMarkMasterRepository(string connectionString, ILogger<ShippingMarkMasterRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// 荷印マスタを取得
    /// </summary>
    public async Task<ShippingMarkMaster?> GetByCodeAsync(string shippingMarkCode)
    {
        const string sql = @"
            SELECT 
                ShippingMarkCode,
                ShippingMarkName,
                SearchKana,
                NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
                DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
                TextValue1, TextValue2, TextValue3, TextValue4, TextValue5
            FROM ShippingMarkMaster
            WHERE ShippingMarkCode = @ShippingMarkCode";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<ShippingMarkMaster>(sql, new { ShippingMarkCode = shippingMarkCode });
    }

    /// <summary>
    /// 全ての荷印マスタを取得
    /// </summary>
    public async Task<IEnumerable<ShippingMarkMaster>> GetAllAsync()
    {
        const string sql = @"
            SELECT 
                ShippingMarkCode,
                ShippingMarkName,
                SearchKana,
                NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
                DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
                TextValue1, TextValue2, TextValue3, TextValue4, TextValue5
            FROM ShippingMarkMaster
            ORDER BY ShippingMarkCode";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<ShippingMarkMaster>(sql);
    }

    /// <summary>
    /// 荷印マスタを登録または更新
    /// </summary>
    public async Task<bool> UpsertAsync(ShippingMarkMaster shippingMark)
    {
        const string sql = @"
            MERGE ShippingMarkMaster AS target
            USING (SELECT @ShippingMarkCode AS ShippingMarkCode) AS source
            ON target.ShippingMarkCode = source.ShippingMarkCode
            WHEN MATCHED THEN
                UPDATE SET 
                    ShippingMarkName = @ShippingMarkName,
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
                    ShippingMarkCode, ShippingMarkName, SearchKana,
                    NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
                    DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
                    TextValue1, TextValue2, TextValue3, TextValue4, TextValue5
                ) VALUES (
                    @ShippingMarkCode, @ShippingMarkName, @SearchKana,
                    @NumericValue1, @NumericValue2, @NumericValue3, @NumericValue4, @NumericValue5,
                    @DateValue1, @DateValue2, @DateValue3, @DateValue4, @DateValue5,
                    @TextValue1, @TextValue2, @TextValue3, @TextValue4, @TextValue5
                );";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.ExecuteAsync(sql, shippingMark);
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "荷印マスタの登録/更新でエラーが発生しました。荷印コード: {Code}", shippingMark.ShippingMarkCode);
            throw;
        }
    }

    /// <summary>
    /// 荷印マスタを一括登録または更新
    /// </summary>
    public async Task<int> BulkUpsertAsync(IEnumerable<ShippingMarkMaster> shippingMarks)
    {
        const string sql = @"
            MERGE ShippingMarkMaster AS target
            USING (SELECT @ShippingMarkCode AS ShippingMarkCode) AS source
            ON target.ShippingMarkCode = source.ShippingMarkCode
            WHEN MATCHED THEN
                UPDATE SET 
                    ShippingMarkName = @ShippingMarkName,
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
                    ShippingMarkCode, ShippingMarkName, SearchKana,
                    NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
                    DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
                    TextValue1, TextValue2, TextValue3, TextValue4, TextValue5
                ) VALUES (
                    @ShippingMarkCode, @ShippingMarkName, @SearchKana,
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
            foreach (var shippingMark in shippingMarks)
            {
                var result = await connection.ExecuteAsync(sql, shippingMark, transaction);
                if (result > 0) count++;
            }
            
            transaction.Commit();
            _logger.LogInformation("荷印マスタを{Count}件一括登録/更新しました", count);
            return count;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "荷印マスタの一括登録/更新でエラーが発生しました");
            throw;
        }
    }

    /// <summary>
    /// 荷印マスタを削除
    /// </summary>
    public async Task<bool> DeleteAsync(string shippingMarkCode)
    {
        const string sql = "DELETE FROM ShippingMarkMaster WHERE ShippingMarkCode = @ShippingMarkCode";

        using var connection = new SqlConnection(_connectionString);
        var result = await connection.ExecuteAsync(sql, new { ShippingMarkCode = shippingMarkCode });
        return result > 0;
    }

    /// <summary>
    /// 荷印コードの存在確認
    /// </summary>
    public async Task<bool> ExistsAsync(string shippingMarkCode)
    {
        const string sql = "SELECT COUNT(1) FROM ShippingMarkMaster WHERE ShippingMarkCode = @ShippingMarkCode";

        using var connection = new SqlConnection(_connectionString);
        var count = await connection.ExecuteScalarAsync<int>(sql, new { ShippingMarkCode = shippingMarkCode });
        return count > 0;
    }

    /// <summary>
    /// 荷印マスタ数を取得
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        const string sql = "SELECT COUNT(*) FROM ShippingMarkMaster";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql);
    }

    /// <summary>
    /// 荷印コードから荷印名を取得
    /// </summary>
    public async Task<string?> GetNameByCodeAsync(string shippingMarkCode)
    {
        if (string.IsNullOrWhiteSpace(shippingMarkCode))
            return null;

        const string sql = "SELECT ShippingMarkName FROM ShippingMarkMaster WHERE ShippingMarkCode = @ShippingMarkCode";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var name = await connection.QueryFirstOrDefaultAsync<string>(sql, new { ShippingMarkCode = shippingMarkCode });
            return name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "荷印名の取得でエラーが発生しました。荷印コード: {Code}", shippingMarkCode);
            return null;
        }
    }
}