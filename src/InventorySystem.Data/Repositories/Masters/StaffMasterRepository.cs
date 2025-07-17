using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces.Masters;

namespace InventorySystem.Data.Repositories.Masters;

/// <summary>
/// 担当者マスタリポジトリ実装
/// </summary>
public class StaffMasterRepository : IStaffMasterRepository
{
    private readonly string _connectionString;
    private readonly ILogger<StaffMasterRepository> _logger;

    public StaffMasterRepository(string connectionString, ILogger<StaffMasterRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<IEnumerable<StaffMaster>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "SELECT * FROM StaffMaster ORDER BY StaffCode";
        
        return await connection.QueryAsync<StaffMaster>(sql);
    }

    public async Task<StaffMaster?> GetByCodeAsync(int staffCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM StaffMaster 
            WHERE StaffCode = @StaffCode";
        
        return await connection.QueryFirstOrDefaultAsync<StaffMaster>(sql, new { StaffCode = staffCode });
    }

    public async Task<IEnumerable<StaffMaster>> GetByDepartmentCodeAsync(int departmentCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM StaffMaster 
            WHERE DepartmentCode = @DepartmentCode
            ORDER BY StaffCode";
        
        return await connection.QueryAsync<StaffMaster>(sql, new { DepartmentCode = departmentCode });
    }

    public async Task<IEnumerable<StaffMaster>> GetByCategoryCodeAsync(int categoryType, int categoryCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var column = categoryType switch
        {
            1 => "Category1Code",
            2 => "Category2Code",
            3 => "Category3Code",
            _ => throw new ArgumentException($"Invalid category type: {categoryType}")
        };

        var sql = $@"
            SELECT * FROM StaffMaster 
            WHERE {column} = @CategoryCode 
            ORDER BY StaffCode";
        
        return await connection.QueryAsync<StaffMaster>(sql, new { CategoryCode = categoryCode });
    }

    public async Task<IEnumerable<StaffMaster>> SearchByKanaAsync(string searchKana)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM StaffMaster 
            WHERE SearchKana LIKE @SearchKana 
            ORDER BY StaffCode";
        
        return await connection.QueryAsync<StaffMaster>(sql, new { SearchKana = $"%{searchKana}%" });
    }

    public async Task<IEnumerable<StaffMaster>> SearchByNameAsync(string staffName)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM StaffMaster 
            WHERE StaffName LIKE @StaffName 
            ORDER BY StaffCode";
        
        return await connection.QueryAsync<StaffMaster>(sql, new { StaffName = $"%{staffName}%" });
    }

    public async Task<bool> ExistsAsync(int staffCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT COUNT(1) FROM StaffMaster 
            WHERE StaffCode = @StaffCode";
        
        var count = await connection.ExecuteScalarAsync<int>(sql, new { StaffCode = staffCode });
        return count > 0;
    }

    public async Task<int> InsertBulkAsync(IEnumerable<StaffMaster> staff)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = @"
                INSERT INTO StaffMaster (
                    StaffCode, StaffName, SearchKana, Category1Code, Category2Code, Category3Code, 
                    DepartmentCode, CreatedAt, UpdatedAt
                ) VALUES (
                    @StaffCode, @StaffName, @SearchKana, @Category1Code, @Category2Code, @Category3Code, 
                    @DepartmentCode, @CreatedAt, @UpdatedAt
                )";

            var count = await connection.ExecuteAsync(sql, staff, transaction);
            
            await transaction.CommitAsync();
            _logger.LogInformation("担当者マスタ一括挿入完了: {Count}件", count);
            
            return count;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "担当者マスタ一括挿入エラー");
            throw;
        }
    }

    public async Task<int> UpdateAsync(StaffMaster staff)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            UPDATE StaffMaster SET
                StaffName = @StaffName,
                SearchKana = @SearchKana,
                Category1Code = @Category1Code,
                Category2Code = @Category2Code,
                Category3Code = @Category3Code,
                DepartmentCode = @DepartmentCode,
                UpdatedAt = GETDATE()
            WHERE StaffCode = @StaffCode";

        return await connection.ExecuteAsync(sql, staff);
    }

    public async Task<int> DeleteAsync(int staffCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            DELETE FROM StaffMaster 
            WHERE StaffCode = @StaffCode";
        
        return await connection.ExecuteAsync(sql, new { StaffCode = staffCode });
    }

    public async Task<int> DeleteAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "DELETE FROM StaffMaster";
        
        var count = await connection.ExecuteAsync(sql);
        _logger.LogInformation("担当者マスタ全削除: {Count}件", count);
        
        return count;
    }

    public async Task<int> UpsertAsync(StaffMaster staff)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            MERGE StaffMaster AS target
            USING (SELECT @StaffCode AS StaffCode) AS source
            ON target.StaffCode = source.StaffCode
            WHEN MATCHED THEN
                UPDATE SET
                    StaffName = @StaffName,
                    SearchKana = @SearchKana,
                    Category1Code = @Category1Code,
                    Category2Code = @Category2Code,
                    Category3Code = @Category3Code,
                    DepartmentCode = @DepartmentCode,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (StaffCode, StaffName, SearchKana, Category1Code, Category2Code, Category3Code, 
                        DepartmentCode, CreatedAt, UpdatedAt)
                VALUES (@StaffCode, @StaffName, @SearchKana, @Category1Code, @Category2Code, @Category3Code, 
                        @DepartmentCode, GETDATE(), GETDATE());";

        return await connection.ExecuteAsync(sql, staff);
    }

    public async Task<int> UpsertBulkAsync(IEnumerable<StaffMaster> staff)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            var totalCount = 0;
            
            // バッチ処理
            const int batchSize = 1000;
            var staffList = staff.ToList();
            
            for (int i = 0; i < staffList.Count; i += batchSize)
            {
                var batch = staffList.Skip(i).Take(batchSize);
                
                foreach (var staffMember in batch)
                {
                    totalCount += await UpsertAsync(staffMember);
                }
            }
            
            await transaction.CommitAsync();
            _logger.LogInformation("担当者マスタ一括更新完了: {Count}件", totalCount);
            
            return totalCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "担当者マスタ一括更新エラー");
            throw;
        }
    }
}