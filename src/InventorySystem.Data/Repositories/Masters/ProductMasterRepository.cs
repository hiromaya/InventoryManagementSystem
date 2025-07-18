using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces.Masters;

namespace InventorySystem.Data.Repositories.Masters;

/// <summary>
/// 商品マスタリポジトリ実装
/// </summary>
public class ProductMasterRepository : IProductMasterRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ProductMasterRepository> _logger;

    public ProductMasterRepository(string connectionString, ILogger<ProductMasterRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<ProductMaster?> GetByCodeAsync(string productCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM ProductMaster 
            WHERE ProductCode = @ProductCode";
        
        return await connection.QueryFirstOrDefaultAsync<ProductMaster>(sql, new { ProductCode = productCode });
    }

    public async Task<IEnumerable<ProductMaster>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "SELECT * FROM ProductMaster ORDER BY ProductCode";
        
        return await connection.QueryAsync<ProductMaster>(sql);
    }

    public async Task<IEnumerable<ProductMaster>> GetStockManagedAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM ProductMaster 
            WHERE IsStockManaged = 1 
            ORDER BY ProductCode";
        
        return await connection.QueryAsync<ProductMaster>(sql);
    }

    public async Task<int> InsertBulkAsync(IEnumerable<ProductMaster> products)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = @"
                INSERT INTO ProductMaster (
                    ProductCode, ProductName, ProductName2, ProductName3, ProductName4, ProductName5,
                    SearchKana, ShortName, PrintCode,
                    ProductCategory1, ProductCategory2, ProductCategory3, ProductCategory4, ProductCategory5,
                    UnitCode, CaseUnitCode, Case2UnitCode, CaseQuantity, Case2Quantity,
                    StandardPrice, CaseStandardPrice, IsStockManaged, TaxRate,
                    CreatedAt, UpdatedAt
                ) VALUES (
                    @ProductCode, @ProductName, @ProductName2, @ProductName3, @ProductName4, @ProductName5,
                    @SearchKana, @ShortName, @PrintCode,
                    @ProductCategory1, @ProductCategory2, @ProductCategory3, @ProductCategory4, @ProductCategory5,
                    @UnitCode, @CaseUnitCode, @Case2UnitCode, @CaseQuantity, @Case2Quantity,
                    @StandardPrice, @CaseStandardPrice, @IsStockManaged, @TaxRate,
                    @CreatedAt, @UpdatedAt
                )";

            var count = await connection.ExecuteAsync(sql, products, transaction);
            
            await transaction.CommitAsync();
            _logger.LogInformation("商品マスタ一括挿入完了: {Count}件", count);
            
            return count;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "商品マスタ一括挿入エラー");
            throw;
        }
    }

    public async Task<int> UpdateAsync(ProductMaster product)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            UPDATE ProductMaster SET
                ProductName = @ProductName,
                ProductName2 = @ProductName2,
                ProductName3 = @ProductName3,
                ProductName4 = @ProductName4,
                ProductName5 = @ProductName5,
                SearchKana = @SearchKana,
                ShortName = @ShortName,
                PrintCode = @PrintCode,
                ProductCategory1 = @ProductCategory1,
                ProductCategory2 = @ProductCategory2,
                ProductCategory3 = @ProductCategory3,
                ProductCategory4 = @ProductCategory4,
                ProductCategory5 = @ProductCategory5,
                UnitCode = @UnitCode,
                CaseUnitCode = @CaseUnitCode,
                Case2UnitCode = @Case2UnitCode,
                CaseQuantity = @CaseQuantity,
                Case2Quantity = @Case2Quantity,
                StandardPrice = @StandardPrice,
                CaseStandardPrice = @CaseStandardPrice,
                IsStockManaged = @IsStockManaged,
                TaxRate = @TaxRate,
                UpdatedAt = GETDATE()
            WHERE ProductCode = @ProductCode";

        return await connection.ExecuteAsync(sql, product);
    }

    public async Task<int> DeleteAsync(string productCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            DELETE FROM ProductMaster 
            WHERE ProductCode = @ProductCode";
        
        return await connection.ExecuteAsync(sql, new { ProductCode = productCode });
    }

    public async Task<bool> ExistsAsync(string productCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT COUNT(1) FROM ProductMaster 
            WHERE ProductCode = @ProductCode";
        
        var count = await connection.ExecuteScalarAsync<int>(sql, new { ProductCode = productCode });
        return count > 0;
    }

    public async Task<IEnumerable<ProductMaster>> SearchByNameAsync(string name)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM ProductMaster 
            WHERE ProductName LIKE @Name 
               OR ProductName2 LIKE @Name 
               OR ProductName3 LIKE @Name 
               OR ProductName4 LIKE @Name 
               OR ProductName5 LIKE @Name 
               OR SearchKana LIKE @Name
            ORDER BY ProductCode";
        
        return await connection.QueryAsync<ProductMaster>(sql, new { Name = $"%{name}%" });
    }

    public async Task<IEnumerable<ProductMaster>> GetByCategoryAsync(string categoryType, string categoryCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var column = categoryType switch
        {
            "1" => "ProductCategory1",
            "2" => "ProductCategory2",
            "3" => "ProductCategory3",
            "4" => "ProductCategory4",
            "5" => "ProductCategory5",
            _ => throw new ArgumentException($"Invalid category type: {categoryType}")
        };

        var sql = $@"
            SELECT * FROM ProductMaster 
            WHERE {column} = @CategoryCode 
            ORDER BY ProductCode";
        
        return await connection.QueryAsync<ProductMaster>(sql, new { CategoryCode = categoryCode });
    }

    public async Task<IEnumerable<ProductMaster>> GetByUnitCodeAsync(string unitCode)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT * FROM ProductMaster 
            WHERE UnitCode = @UnitCode 
               OR CaseUnitCode = @UnitCode 
               OR Case2UnitCode = @UnitCode
            ORDER BY ProductCode";
        
        return await connection.QueryAsync<ProductMaster>(sql, new { UnitCode = unitCode });
    }

    public async Task<int> DeleteAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = "DELETE FROM ProductMaster";
        
        var count = await connection.ExecuteAsync(sql);
        _logger.LogInformation("商品マスタ全削除: {Count}件", count);
        
        return count;
    }

    public async Task<int> UpsertAsync(ProductMaster product)
    {
        using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            MERGE ProductMaster AS target
            USING (SELECT @ProductCode AS ProductCode) AS source
            ON target.ProductCode = source.ProductCode
            WHEN MATCHED THEN
                UPDATE SET
                    ProductName = @ProductName,
                    ProductName2 = @ProductName2,
                    ProductName3 = @ProductName3,
                    ProductName4 = @ProductName4,
                    ProductName5 = @ProductName5,
                    SearchKana = @SearchKana,
                    ShortName = @ShortName,
                    PrintCode = @PrintCode,
                    ProductCategory1 = @ProductCategory1,
                    ProductCategory2 = @ProductCategory2,
                    ProductCategory3 = @ProductCategory3,
                    ProductCategory4 = @ProductCategory4,
                    ProductCategory5 = @ProductCategory5,
                    UnitCode = @UnitCode,
                    CaseUnitCode = @CaseUnitCode,
                    Case2UnitCode = @Case2UnitCode,
                    CaseQuantity = @CaseQuantity,
                    Case2Quantity = @Case2Quantity,
                    StandardPrice = @StandardPrice,
                    CaseStandardPrice = @CaseStandardPrice,
                    IsStockManaged = @IsStockManaged,
                    TaxRate = @TaxRate,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (
                    ProductCode, ProductName, ProductName2, ProductName3, ProductName4, ProductName5,
                    SearchKana, ShortName, PrintCode,
                    ProductCategory1, ProductCategory2, ProductCategory3, ProductCategory4, ProductCategory5,
                    UnitCode, CaseUnitCode, Case2UnitCode, CaseQuantity, Case2Quantity,
                    StandardPrice, CaseStandardPrice, IsStockManaged, TaxRate,
                    CreatedAt, UpdatedAt
                ) VALUES (
                    @ProductCode, @ProductName, @ProductName2, @ProductName3, @ProductName4, @ProductName5,
                    @SearchKana, @ShortName, @PrintCode,
                    @ProductCategory1, @ProductCategory2, @ProductCategory3, @ProductCategory4, @ProductCategory5,
                    @UnitCode, @CaseUnitCode, @Case2UnitCode, @CaseQuantity, @Case2Quantity,
                    @StandardPrice, @CaseStandardPrice, @IsStockManaged, @TaxRate,
                    GETDATE(), GETDATE()
                );";

        return await connection.ExecuteAsync(sql, product);
    }

    public async Task<int> UpsertBulkAsync(IEnumerable<ProductMaster> products)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            var totalCount = 0;
            
            // バッチ処理
            const int batchSize = 1000;
            var productList = products.ToList();
            
            for (int i = 0; i < productList.Count; i += batchSize)
            {
                var batch = productList.Skip(i).Take(batchSize);
                
                foreach (var product in batch)
                {
                    totalCount += await UpsertAsync(product);
                }
            }
            
            await transaction.CommitAsync();
            _logger.LogInformation("商品マスタ一括更新完了: {Count}件", totalCount);
            
            return totalCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "商品マスタ一括更新エラー");
            throw;
        }
    }
}