using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces.Masters;

namespace InventorySystem.Data.Repositories.Masters;

/// <summary>
/// 仕入先マスタリポジトリ実装
/// </summary>
public class SupplierMasterRepository : ISupplierMasterRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SupplierMasterRepository> _logger;

    public SupplierMasterRepository(string connectionString, ILogger<SupplierMasterRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<SupplierMaster?> GetByCodeAsync(string supplierCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM SupplierMaster 
            WHERE SupplierCode = @SupplierCode";
        
        return await connection.QueryFirstOrDefaultAsync<SupplierMaster>(sql, new { SupplierCode = supplierCode });
    }

    public async Task<IEnumerable<SupplierMaster>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "SELECT * FROM SupplierMaster ORDER BY SupplierCode";
        
        return await connection.QueryAsync<SupplierMaster>(sql);
    }

    public async Task<IEnumerable<SupplierMaster>> GetActiveAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM SupplierMaster 
            WHERE IsActive = 1 
            ORDER BY SupplierCode";
        
        return await connection.QueryAsync<SupplierMaster>(sql);
    }

    public async Task<int> InsertBulkAsync(IEnumerable<SupplierMaster> suppliers)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = @"
                INSERT INTO SupplierMaster (
                    SupplierCode, SupplierName, SupplierName2, SearchKana, ShortName,
                    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
                    SupplierCategory1, SupplierCategory2, SupplierCategory3,
                    PaymentCode, IsActive, CreatedDate, UpdatedDate
                ) VALUES (
                    @SupplierCode, @SupplierName, @SupplierName2, @SearchKana, @ShortName,
                    @PostalCode, @Address1, @Address2, @Address3, @PhoneNumber, @FaxNumber,
                    @SupplierCategory1, @SupplierCategory2, @SupplierCategory3,
                    @PaymentCode, @IsActive, @CreatedDate, @UpdatedDate
                )";

            var count = await connection.ExecuteAsync(sql, suppliers, transaction);
            
            await transaction.CommitAsync();
            _logger.LogInformation("仕入先マスタ一括挿入完了: {Count}件", count);
            
            return count;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "仕入先マスタ一括挿入エラー");
            throw;
        }
    }

    public async Task<int> UpdateAsync(SupplierMaster supplier)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            UPDATE SupplierMaster SET
                SupplierName = @SupplierName,
                SupplierName2 = @SupplierName2,
                SearchKana = @SearchKana,
                ShortName = @ShortName,
                PostalCode = @PostalCode,
                Address1 = @Address1,
                Address2 = @Address2,
                Address3 = @Address3,
                PhoneNumber = @PhoneNumber,
                FaxNumber = @FaxNumber,
                SupplierCategory1 = @SupplierCategory1,
                SupplierCategory2 = @SupplierCategory2,
                SupplierCategory3 = @SupplierCategory3,
                PaymentCode = @PaymentCode,
                IsActive = @IsActive,
                UpdatedDate = GETDATE()
            WHERE SupplierCode = @SupplierCode";

        return await connection.ExecuteAsync(sql, supplier);
    }

    public async Task<int> DeleteAsync(string supplierCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        // 論理削除
        const string sql = @"
            UPDATE SupplierMaster 
            SET IsActive = 0, UpdatedDate = GETDATE() 
            WHERE SupplierCode = @SupplierCode";
        
        return await connection.ExecuteAsync(sql, new { SupplierCode = supplierCode });
    }

    public async Task<bool> ExistsAsync(string supplierCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT COUNT(1) FROM SupplierMaster 
            WHERE SupplierCode = @SupplierCode";
        
        var count = await connection.ExecuteScalarAsync<int>(sql, new { SupplierCode = supplierCode });
        return count > 0;
    }

    public async Task<IEnumerable<SupplierMaster>> SearchByNameAsync(string name)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM SupplierMaster 
            WHERE SupplierName LIKE @Name 
               OR SupplierName2 LIKE @Name 
               OR SearchKana LIKE @Name
            ORDER BY SupplierCode";
        
        return await connection.QueryAsync<SupplierMaster>(sql, new { Name = $"%{name}%" });
    }

    public async Task<IEnumerable<SupplierMaster>> GetByPaymentCodeAsync(string paymentCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM SupplierMaster 
            WHERE PaymentCode = @PaymentCode 
            ORDER BY SupplierCode";
        
        return await connection.QueryAsync<SupplierMaster>(sql, new { PaymentCode = paymentCode });
    }

    public async Task<IEnumerable<SupplierMaster>> GetIncentiveTargetsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM SupplierMaster 
            WHERE SupplierCategory1 = '01' 
               AND IsActive = 1
            ORDER BY SupplierCode";
        
        return await connection.QueryAsync<SupplierMaster>(sql);
    }

    public async Task<int> DeleteAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "DELETE FROM SupplierMaster";
        
        var count = await connection.ExecuteAsync(sql);
        _logger.LogInformation("仕入先マスタ全削除: {Count}件", count);
        
        return count;
    }

    public async Task<int> UpsertAsync(SupplierMaster supplier)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            MERGE SupplierMaster AS target
            USING (SELECT @SupplierCode AS SupplierCode) AS source
            ON target.SupplierCode = source.SupplierCode
            WHEN MATCHED THEN
                UPDATE SET
                    SupplierName = @SupplierName,
                    SupplierName2 = @SupplierName2,
                    SearchKana = @SearchKana,
                    ShortName = @ShortName,
                    PostalCode = @PostalCode,
                    Address1 = @Address1,
                    Address2 = @Address2,
                    Address3 = @Address3,
                    PhoneNumber = @PhoneNumber,
                    FaxNumber = @FaxNumber,
                    SupplierCategory1 = @SupplierCategory1,
                    SupplierCategory2 = @SupplierCategory2,
                    SupplierCategory3 = @SupplierCategory3,
                    PaymentCode = @PaymentCode,
                    IsActive = @IsActive,
                    UpdatedDate = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (
                    SupplierCode, SupplierName, SupplierName2, SearchKana, ShortName,
                    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
                    SupplierCategory1, SupplierCategory2, SupplierCategory3,
                    PaymentCode, IsActive, CreatedDate, UpdatedDate
                ) VALUES (
                    @SupplierCode, @SupplierName, @SupplierName2, @SearchKana, @ShortName,
                    @PostalCode, @Address1, @Address2, @Address3, @PhoneNumber, @FaxNumber,
                    @SupplierCategory1, @SupplierCategory2, @SupplierCategory3,
                    @PaymentCode, @IsActive, GETDATE(), GETDATE()
                );";

        return await connection.ExecuteAsync(sql, supplier);
    }

    public async Task<int> UpsertBulkAsync(IEnumerable<SupplierMaster> suppliers)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            var totalCount = 0;
            
            // バッチ処理
            const int batchSize = 1000;
            var supplierList = suppliers.ToList();
            
            for (int i = 0; i < supplierList.Count; i += batchSize)
            {
                var batch = supplierList.Skip(i).Take(batchSize);
                
                foreach (var supplier in batch)
                {
                    totalCount += await UpsertAsync(supplier);
                }
            }
            
            await transaction.CommitAsync();
            _logger.LogInformation("仕入先マスタ一括更新完了: {Count}件", totalCount);
            
            return totalCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "仕入先マスタ一括更新エラー");
            throw;
        }
    }
}