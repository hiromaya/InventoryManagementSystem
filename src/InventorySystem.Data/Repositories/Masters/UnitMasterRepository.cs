using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces.Masters;

namespace InventorySystem.Data.Repositories.Masters;

/// <summary>
/// 単位マスタリポジトリ実装
/// </summary>
public class UnitMasterRepository : IUnitMasterRepository
{
    private readonly string _connectionString;
    private readonly ILogger<UnitMasterRepository> _logger;

    public UnitMasterRepository(string connectionString, ILogger<UnitMasterRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<IEnumerable<UnitMaster>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "SELECT * FROM UnitMaster ORDER BY UnitCode";
        
        return await connection.QueryAsync<UnitMaster>(sql);
    }

    public async Task<UnitMaster?> GetByCodeAsync(int unitCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM UnitMaster 
            WHERE UnitCode = @UnitCode";
        
        return await connection.QueryFirstOrDefaultAsync<UnitMaster>(sql, new { UnitCode = unitCode });
    }

    public async Task<IEnumerable<UnitMaster>> SearchByKanaAsync(string searchKana)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM UnitMaster 
            WHERE SearchKana LIKE @SearchKana 
            ORDER BY UnitCode";
        
        return await connection.QueryAsync<UnitMaster>(sql, new { SearchKana = $"%{searchKana}%" });
    }

    public async Task<IEnumerable<UnitMaster>> SearchByNameAsync(string unitName)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM UnitMaster 
            WHERE UnitName LIKE @UnitName 
            ORDER BY UnitCode";
        
        return await connection.QueryAsync<UnitMaster>(sql, new { UnitName = $"%{unitName}%" });
    }

    public async Task<bool> ExistsAsync(int unitCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT COUNT(1) FROM UnitMaster 
            WHERE UnitCode = @UnitCode";
        
        var count = await connection.ExecuteScalarAsync<int>(sql, new { UnitCode = unitCode });
        return count > 0;
    }

    public async Task<int> InsertBulkAsync(IEnumerable<UnitMaster> units)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = @"
                INSERT INTO UnitMaster (
                    UnitCode, UnitName, SearchKana, CreatedAt, UpdatedAt
                ) VALUES (
                    @UnitCode, @UnitName, @SearchKana, @CreatedAt, @UpdatedAt
                )";

            var count = await connection.ExecuteAsync(sql, units, transaction);
            
            await transaction.CommitAsync();
            _logger.LogInformation("単位マスタ一括挿入完了: {Count}件", count);
            
            return count;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "単位マスタ一括挿入エラー");
            throw;
        }
    }

    public async Task<int> UpdateAsync(UnitMaster unit)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            UPDATE UnitMaster SET
                UnitName = @UnitName,
                SearchKana = @SearchKana,
                UpdatedAt = GETDATE()
            WHERE UnitCode = @UnitCode";

        return await connection.ExecuteAsync(sql, unit);
    }

    public async Task<int> DeleteAsync(int unitCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            DELETE FROM UnitMaster 
            WHERE UnitCode = @UnitCode";
        
        return await connection.ExecuteAsync(sql, new { UnitCode = unitCode });
    }

    public async Task<int> DeleteAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "DELETE FROM UnitMaster";
        
        var count = await connection.ExecuteAsync(sql);
        _logger.LogInformation("単位マスタ全削除: {Count}件", count);
        
        return count;
    }

    public async Task<int> UpsertAsync(UnitMaster unit)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            MERGE UnitMaster AS target
            USING (SELECT @UnitCode AS UnitCode) AS source
            ON target.UnitCode = source.UnitCode
            WHEN MATCHED THEN
                UPDATE SET
                    UnitName = @UnitName,
                    SearchKana = @SearchKana,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (UnitCode, UnitName, SearchKana, CreatedAt, UpdatedAt)
                VALUES (@UnitCode, @UnitName, @SearchKana, GETDATE(), GETDATE());";

        return await connection.ExecuteAsync(sql, unit);
    }

    public async Task<int> UpsertBulkAsync(IEnumerable<UnitMaster> units)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            var totalCount = 0;
            
            // バッチ処理
            const int batchSize = 1000;
            var unitList = units.ToList();
            
            for (int i = 0; i < unitList.Count; i += batchSize)
            {
                var batch = unitList.Skip(i).Take(batchSize);
                
                foreach (var unit in batch)
                {
                    totalCount += await UpsertAsync(unit);
                }
            }
            
            await transaction.CommitAsync();
            _logger.LogInformation("単位マスタ一括更新完了: {Count}件", totalCount);
            
            return totalCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "単位マスタ一括更新エラー");
            throw;
        }
    }
}