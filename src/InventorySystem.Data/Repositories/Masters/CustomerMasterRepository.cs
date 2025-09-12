using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces.Masters;
using System.Data;

namespace InventorySystem.Data.Repositories.Masters;

/// <summary>
/// 得意先マスタリポジトリ実装
/// </summary>
public class CustomerMasterRepository : ICustomerMasterRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CustomerMasterRepository> _logger;

    public CustomerMasterRepository(string connectionString, ILogger<CustomerMasterRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<CustomerMaster?> GetByCodeAsync(string customerCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM CustomerMaster 
            WHERE CustomerCode = @CustomerCode";
        
        return await connection.QueryFirstOrDefaultAsync<CustomerMaster>(sql, new { CustomerCode = customerCode });
    }

    public async Task<IEnumerable<CustomerMaster>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "SELECT * FROM CustomerMaster ORDER BY CustomerCode";
        
        return await connection.QueryAsync<CustomerMaster>(sql);
    }

    public async Task<IEnumerable<CustomerMaster>> GetActiveAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM CustomerMaster 
            WHERE IsActive = 1 
            ORDER BY CustomerCode";
        
        return await connection.QueryAsync<CustomerMaster>(sql);
    }

    public async Task<int> InsertBulkAsync(IEnumerable<CustomerMaster> customers)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = @"
                INSERT INTO CustomerMaster (
                    CustomerCode, CustomerName, CustomerName2, SearchKana, ShortName,
                    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
                    CustomerCategory1, CustomerCategory2, CustomerCategory3, CustomerCategory4, CustomerCategory5,
                    WalkingRate, BillingCode, IsActive, CreatedAt, UpdatedAt
                ) VALUES (
                    @CustomerCode, @CustomerName, @CustomerName2, @SearchKana, @ShortName,
                    @PostalCode, @Address1, @Address2, @Address3, @PhoneNumber, @FaxNumber,
                    @CustomerCategory1, @CustomerCategory2, @CustomerCategory3, @CustomerCategory4, @CustomerCategory5,
                    @WalkingRate, @BillingCode, @IsActive, @CreatedAt, @UpdatedAt
                )";

            var count = await connection.ExecuteAsync(sql, customers, transaction);
            
            await transaction.CommitAsync();
            _logger.LogInformation("得意先マスタ一括挿入完了: {Count}件", count);
            
            return count;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "得意先マスタ一括挿入エラー");
            throw;
        }
    }

    public async Task<int> UpdateAsync(CustomerMaster customer)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            UPDATE CustomerMaster SET
                CustomerName = @CustomerName,
                CustomerName2 = @CustomerName2,
                SearchKana = @SearchKana,
                ShortName = @ShortName,
                PostalCode = @PostalCode,
                Address1 = @Address1,
                Address2 = @Address2,
                Address3 = @Address3,
                PhoneNumber = @PhoneNumber,
                FaxNumber = @FaxNumber,
                CustomerCategory1 = @CustomerCategory1,
                CustomerCategory2 = @CustomerCategory2,
                CustomerCategory3 = @CustomerCategory3,
                CustomerCategory4 = @CustomerCategory4,
                CustomerCategory5 = @CustomerCategory5,
                WalkingRate = @WalkingRate,
                BillingCode = @BillingCode,
                IsActive = @IsActive,
                UpdatedAt = GETDATE()
            WHERE CustomerCode = @CustomerCode";

        return await connection.ExecuteAsync(sql, customer);
    }

    public async Task<int> DeleteAsync(string customerCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        // 論理削除
        const string sql = @"
            UPDATE CustomerMaster 
            SET IsActive = 0, UpdatedDate = GETDATE() 
            WHERE CustomerCode = @CustomerCode";
        
        return await connection.ExecuteAsync(sql, new { CustomerCode = customerCode });
    }

    public async Task<bool> ExistsAsync(string customerCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT COUNT(1) FROM CustomerMaster 
            WHERE CustomerCode = @CustomerCode";
        
        var count = await connection.ExecuteScalarAsync<int>(sql, new { CustomerCode = customerCode });
        return count > 0;
    }

    public async Task<IEnumerable<CustomerMaster>> SearchByNameAsync(string name)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM CustomerMaster 
            WHERE CustomerName LIKE @Name 
               OR CustomerName2 LIKE @Name 
               OR SearchKana LIKE @Name
            ORDER BY CustomerCode";
        
        return await connection.QueryAsync<CustomerMaster>(sql, new { Name = $"%{name}%" });
    }

    public async Task<IEnumerable<CustomerMaster>> GetByBillingCodeAsync(string billingCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM CustomerMaster 
            WHERE BillingCode = @BillingCode 
            ORDER BY CustomerCode";
        
        return await connection.QueryAsync<CustomerMaster>(sql, new { BillingCode = billingCode });
    }

    public async Task<IEnumerable<CustomerMaster>> GetByCategoryAsync(string categoryType, string categoryCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var column = categoryType switch
        {
            "1" => "CustomerCategory1",
            "2" => "CustomerCategory2",
            "3" => "CustomerCategory3",
            "4" => "CustomerCategory4",
            "5" => "CustomerCategory5",
            _ => throw new ArgumentException($"Invalid category type: {categoryType}")
        };

        var sql = $@"
            SELECT * FROM CustomerMaster 
            WHERE {column} = @CategoryCode 
            ORDER BY CustomerCode";
        
        return await connection.QueryAsync<CustomerMaster>(sql, new { CategoryCode = categoryCode });
    }

    public async Task<int> DeleteAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "DELETE FROM CustomerMaster";
        
        var count = await connection.ExecuteAsync(sql);
        _logger.LogInformation("得意先マスタ全削除: {Count}件", count);
        
        return count;
    }

    public async Task<int> UpsertAsync(CustomerMaster customer)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            MERGE CustomerMaster AS target
            USING (SELECT @CustomerCode AS CustomerCode) AS source
            ON target.CustomerCode = source.CustomerCode
            WHEN MATCHED THEN
                UPDATE SET
                    CustomerName = @CustomerName,
                    CustomerName2 = @CustomerName2,
                    SearchKana = @SearchKana,
                    ShortName = @ShortName,
                    PostalCode = @PostalCode,
                    Address1 = @Address1,
                    Address2 = @Address2,
                    Address3 = @Address3,
                    PhoneNumber = @PhoneNumber,
                    FaxNumber = @FaxNumber,
                    CustomerCategory1 = @CustomerCategory1,
                    CustomerCategory2 = @CustomerCategory2,
                    CustomerCategory3 = @CustomerCategory3,
                    CustomerCategory4 = @CustomerCategory4,
                    CustomerCategory5 = @CustomerCategory5,
                    WalkingRate = @WalkingRate,
                    BillingCode = @BillingCode,
                    IsActive = @IsActive,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (
                    CustomerCode, CustomerName, CustomerName2, SearchKana, ShortName,
                    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
                    CustomerCategory1, CustomerCategory2, CustomerCategory3, CustomerCategory4, CustomerCategory5,
                    WalkingRate, BillingCode, IsActive, CreatedAt, UpdatedAt
                ) VALUES (
                    @CustomerCode, @CustomerName, @CustomerName2, @SearchKana, @ShortName,
                    @PostalCode, @Address1, @Address2, @Address3, @PhoneNumber, @FaxNumber,
                    @CustomerCategory1, @CustomerCategory2, @CustomerCategory3, @CustomerCategory4, @CustomerCategory5,
                    @WalkingRate, @BillingCode, @IsActive, GETDATE(), GETDATE()
                );";

        return await connection.ExecuteAsync(sql, customer);
    }

    public async Task<int> UpsertBulkAsync(IEnumerable<CustomerMaster> customers)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            var totalCount = 0;
            
            // バッチ処理
            const int batchSize = 1000;
            var customerList = customers.ToList();
            
            for (int i = 0; i < customerList.Count; i += batchSize)
            {
                var batch = customerList.Skip(i).Take(batchSize);
                
                foreach (var customer in batch)
                {
                    totalCount += await UpsertAsync(customer);
                }
            }
            
            await transaction.CommitAsync();
            _logger.LogInformation("得意先マスタ一括更新完了: {Count}件", totalCount);
            
            return totalCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "得意先マスタ一括更新エラー");
            throw;
        }
    }
}