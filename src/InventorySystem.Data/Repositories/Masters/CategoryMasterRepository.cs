using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces.Masters;

namespace InventorySystem.Data.Repositories.Masters;

/// <summary>
/// 分類マスタの汎用リポジトリ実装
/// </summary>
/// <typeparam name="T">分類マスタエンティティタイプ</typeparam>
public class CategoryMasterRepository<T> : ICategoryMasterRepository<T> where T : CategoryMasterBase, new()
{
    private readonly string _connectionString;
    private readonly ILogger<CategoryMasterRepository<T>> _logger;
    private readonly string _tableName;

    public CategoryMasterRepository(string connectionString, ILogger<CategoryMasterRepository<T>> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
        _tableName = GetTableName();
    }

    /// <summary>
    /// エンティティタイプからテーブル名を取得
    /// </summary>
    private static string GetTableName()
    {
        return typeof(T).Name;
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = $"SELECT * FROM {_tableName} ORDER BY CategoryCode";
        
        return await connection.QueryAsync<T>(sql);
    }

    public async Task<T?> GetByCodeAsync(string categoryCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = $@"
            SELECT * FROM {_tableName} 
            WHERE CategoryCode = @CategoryCode";
        
        return await connection.QueryFirstOrDefaultAsync<T>(sql, new { CategoryCode = categoryCode });
    }

    public async Task<IEnumerable<T>> SearchByKanaAsync(string searchKana)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = $@"
            SELECT * FROM {_tableName} 
            WHERE SearchKana LIKE @SearchKana 
            ORDER BY CategoryCode";
        
        return await connection.QueryAsync<T>(sql, new { SearchKana = $"%{searchKana}%" });
    }

    public async Task<IEnumerable<T>> SearchByNameAsync(string categoryName)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = $@"
            SELECT * FROM {_tableName} 
            WHERE CategoryName LIKE @CategoryName 
            ORDER BY CategoryCode";
        
        return await connection.QueryAsync<T>(sql, new { CategoryName = $"%{categoryName}%" });
    }

    public async Task<bool> ExistsAsync(string categoryCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = $@"
            SELECT COUNT(1) FROM {_tableName} 
            WHERE CategoryCode = @CategoryCode";
        
        var count = await connection.ExecuteScalarAsync<int>(sql, new { CategoryCode = categoryCode });
        return count > 0;
    }

    public async Task<int> InsertBulkAsync(IEnumerable<T> categories)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            var sql = $@"
                INSERT INTO {_tableName} (
                    CategoryCode, CategoryName, SearchKana, CreatedAt, UpdatedAt
                ) VALUES (
                    @CategoryCode, @CategoryName, @SearchKana, @CreatedAt, @UpdatedAt
                )";

            var count = await connection.ExecuteAsync(sql, categories, transaction);
            
            await transaction.CommitAsync();
            _logger.LogInformation("{TableName}一括挿入完了: {Count}件", _tableName, count);
            
            return count;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "{TableName}一括挿入エラー", _tableName);
            throw;
        }
    }

    public async Task<int> UpdateAsync(T category)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = $@"
            UPDATE {_tableName} SET
                CategoryName = @CategoryName,
                SearchKana = @SearchKana,
                UpdatedAt = GETDATE()
            WHERE CategoryCode = @CategoryCode";

        return await connection.ExecuteAsync(sql, category);
    }

    public async Task<int> DeleteAsync(string categoryCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = $@"
            DELETE FROM {_tableName} 
            WHERE CategoryCode = @CategoryCode";
        
        return await connection.ExecuteAsync(sql, new { CategoryCode = categoryCode });
    }

    public async Task<int> DeleteAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = $"DELETE FROM {_tableName}";
        
        var count = await connection.ExecuteAsync(sql);
        _logger.LogInformation("{TableName}全削除: {Count}件", _tableName, count);
        
        return count;
    }

    public async Task<int> UpsertAsync(T category)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = $@"
            MERGE {_tableName} AS target
            USING (SELECT @CategoryCode AS CategoryCode) AS source
            ON target.CategoryCode = source.CategoryCode
            WHEN MATCHED THEN
                UPDATE SET
                    CategoryName = @CategoryName,
                    SearchKana = @SearchKana,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (CategoryCode, CategoryName, SearchKana, CreatedAt, UpdatedAt)
                VALUES (@CategoryCode, @CategoryName, @SearchKana, GETDATE(), GETDATE());";

        return await connection.ExecuteAsync(sql, category);
    }

    public async Task<int> UpsertBulkAsync(IEnumerable<T> categories)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            var totalCount = 0;
            
            // バッチ処理
            const int batchSize = 1000;
            var categoryList = categories.ToList();
            
            for (int i = 0; i < categoryList.Count; i += batchSize)
            {
                var batch = categoryList.Skip(i).Take(batchSize);
                
                foreach (var category in batch)
                {
                    totalCount += await UpsertAsync(category);
                }
            }
            
            await transaction.CommitAsync();
            _logger.LogInformation("{TableName}一括更新完了: {Count}件", _tableName, totalCount);
            
            return totalCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "{TableName}一括更新エラー", _tableName);
            throw;
        }
    }
}