using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories;

public class InventoryRepository : BaseRepository, IInventoryRepository
{
    public InventoryRepository(string connectionString, ILogger<InventoryRepository> logger)
        : base(connectionString, logger)
    {
    }

    public async Task<IEnumerable<InventoryMaster>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                DailyFlag, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
                DataSetId
            FROM InventoryMaster 
            WHERE JobDate = @JobDate
            ORDER BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName";

        try
        {
            using var connection = CreateConnection();
            var inventories = await connection.QueryAsync<dynamic>(sql, new { JobDate = jobDate });
            
            return inventories.Select(MapToInventoryMaster);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByJobDateAsync), new { jobDate });
            throw;
        }
    }

    public async Task<InventoryMaster?> GetByKeyAsync(InventoryKey key, DateTime jobDate)
    {
        const string sql = @"
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                DailyFlag, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
                DataSetId
            FROM InventoryMaster 
            WHERE ProductCode = @ProductCode 
                AND GradeCode = @GradeCode 
                AND ClassCode = @ClassCode 
                AND ShippingMarkCode = @ShippingMarkCode 
                AND ShippingMarkName = @ShippingMarkName 
                AND JobDate = @JobDate";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new
            {
                key.ProductCode,
                key.GradeCode,
                key.ClassCode,
                key.ShippingMarkCode,
                key.ShippingMarkName,
                JobDate = jobDate
            });

            return result != null ? MapToInventoryMaster(result) : null;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByKeyAsync), new { key, jobDate });
            throw;
        }
    }

    public async Task<int> CreateAsync(InventoryMaster inventory)
    {
        const string sql = @"
            INSERT INTO InventoryMaster (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                DailyFlag, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
                DataSetId
            ) VALUES (
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @ProductName, @Unit, @StandardPrice, @ProductCategory1, @ProductCategory2,
                @JobDate, @CreatedDate, @UpdatedDate,
                @CurrentStock, @CurrentStockAmount, @DailyStock, @DailyStockAmount,
                @DailyFlag, @DailyGrossProfit, @DailyAdjustmentAmount, @DailyProcessingCost, @FinalGrossProfit,
                @DataSetId
            )";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, MapFromInventoryMaster(inventory));
            
            LogInfo($"Created inventory record", new { inventory.Key, inventory.JobDate });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(CreateAsync), inventory);
            throw;
        }
    }

    public async Task<int> UpdateAsync(InventoryMaster inventory)
    {
        const string sql = @"
            UPDATE InventoryMaster SET
                ProductName = @ProductName,
                Unit = @Unit,
                StandardPrice = @StandardPrice,
                ProductCategory1 = @ProductCategory1,
                ProductCategory2 = @ProductCategory2,
                UpdatedDate = @UpdatedDate,
                CurrentStock = @CurrentStock,
                CurrentStockAmount = @CurrentStockAmount,
                DailyStock = @DailyStock,
                DailyStockAmount = @DailyStockAmount,
                DailyFlag = @DailyFlag,
                DailyGrossProfit = @DailyGrossProfit,
                DailyAdjustmentAmount = @DailyAdjustmentAmount,
                DailyProcessingCost = @DailyProcessingCost,
                FinalGrossProfit = @FinalGrossProfit,
                DataSetId = @DataSetId
            WHERE ProductCode = @ProductCode 
                AND GradeCode = @GradeCode 
                AND ClassCode = @ClassCode 
                AND ShippingMarkCode = @ShippingMarkCode 
                AND ShippingMarkName = @ShippingMarkName 
                AND JobDate = @JobDate";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, MapFromInventoryMaster(inventory));
            
            LogInfo($"Updated inventory record", new { inventory.Key, inventory.JobDate });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateAsync), inventory);
            throw;
        }
    }

    public async Task<int> DeleteByJobDateAsync(DateTime jobDate)
    {
        const string sql = "DELETE FROM InventoryMaster WHERE JobDate = @JobDate";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            
            LogInfo($"Deleted {result} inventory records", new { jobDate });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeleteByJobDateAsync), new { jobDate });
            throw;
        }
    }

    public async Task<int> ClearDailyFlagAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE InventoryMaster 
            SET DailyFlag = '9', UpdatedDate = GETDATE()
            WHERE JobDate = @JobDate";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            
            LogInfo($"Cleared daily flag for {result} records", new { jobDate });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(ClearDailyFlagAsync), new { jobDate });
            throw;
        }
    }

    public async Task<int> BulkInsertAsync(IEnumerable<InventoryMaster> inventories)
    {
        const string sql = @"
            INSERT INTO InventoryMaster (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                DailyFlag, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
                DataSetId
            ) VALUES (
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @ProductName, @Unit, @StandardPrice, @ProductCategory1, @ProductCategory2,
                @JobDate, @CreatedDate, @UpdatedDate,
                @CurrentStock, @CurrentStockAmount, @DailyStock, @DailyStockAmount,
                @DailyFlag, @DailyGrossProfit, @DailyAdjustmentAmount, @DailyProcessingCost, @FinalGrossProfit,
                @DataSetId
            )";

        try
        {
            using var connection = CreateConnection();
            var parameters = inventories.Select(MapFromInventoryMaster);
            var result = await connection.ExecuteAsync(sql, parameters);
            
            LogInfo($"Bulk inserted {result} inventory records");
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(BulkInsertAsync), new { Count = inventories.Count() });
            throw;
        }
    }

    private static InventoryMaster MapToInventoryMaster(dynamic row)
    {
        return new InventoryMaster
        {
            Key = new InventoryKey
            {
                ProductCode = row.ProductCode,
                GradeCode = row.GradeCode,
                ClassCode = row.ClassCode,
                ShippingMarkCode = row.ShippingMarkCode,
                ShippingMarkName = row.ShippingMarkName
            },
            ProductName = row.ProductName,
            Unit = row.Unit,
            StandardPrice = row.StandardPrice,
            ProductCategory1 = row.ProductCategory1,
            ProductCategory2 = row.ProductCategory2,
            JobDate = row.JobDate,
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate,
            CurrentStock = row.CurrentStock,
            CurrentStockAmount = row.CurrentStockAmount,
            DailyStock = row.DailyStock,
            DailyStockAmount = row.DailyStockAmount,
            DailyFlag = row.DailyFlag,
            DailyGrossProfit = row.DailyGrossProfit,
            DailyAdjustmentAmount = row.DailyAdjustmentAmount,
            DailyProcessingCost = row.DailyProcessingCost,
            FinalGrossProfit = row.FinalGrossProfit,
            DataSetId = row.DataSetId
        };
    }

    private static object MapFromInventoryMaster(InventoryMaster inventory)
    {
        return new
        {
            ProductCode = inventory.Key.ProductCode,
            GradeCode = inventory.Key.GradeCode,
            ClassCode = inventory.Key.ClassCode,
            ShippingMarkCode = inventory.Key.ShippingMarkCode,
            ShippingMarkName = inventory.Key.ShippingMarkName,
            inventory.ProductName,
            inventory.Unit,
            inventory.StandardPrice,
            inventory.ProductCategory1,
            inventory.ProductCategory2,
            inventory.JobDate,
            inventory.CreatedDate,
            inventory.UpdatedDate,
            inventory.CurrentStock,
            inventory.CurrentStockAmount,
            inventory.DailyStock,
            inventory.DailyStockAmount,
            inventory.DailyFlag,
            inventory.DailyGrossProfit,
            inventory.DailyAdjustmentAmount,
            inventory.DailyProcessingCost,
            inventory.FinalGrossProfit,
            inventory.DataSetId
        };
    }
}