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
                DataSetId, PreviousMonthQuantity, PreviousMonthAmount
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
                DataSetId, PreviousMonthQuantity, PreviousMonthAmount
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
                DataSetId, PreviousMonthQuantity, PreviousMonthAmount
            ) VALUES (
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @ProductName, @Unit, @StandardPrice, @ProductCategory1, @ProductCategory2,
                @JobDate, @CreatedDate, @UpdatedDate,
                @CurrentStock, @CurrentStockAmount, @DailyStock, @DailyStockAmount,
                @DailyFlag, @DailyGrossProfit, @DailyAdjustmentAmount, @DailyProcessingCost, @FinalGrossProfit,
                @DataSetId, @PreviousMonthQuantity, @PreviousMonthAmount
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
                DataSetId = @DataSetId,
                PreviousMonthQuantity = @PreviousMonthQuantity,
                PreviousMonthAmount = @PreviousMonthAmount
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
                DataSetId, PreviousMonthQuantity, PreviousMonthAmount
            ) VALUES (
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @ProductName, @Unit, @StandardPrice, @ProductCategory1, @ProductCategory2,
                @JobDate, @CreatedDate, @UpdatedDate,
                @CurrentStock, @CurrentStockAmount, @DailyStock, @DailyStockAmount,
                @DailyFlag, @DailyGrossProfit, @DailyAdjustmentAmount, @DailyProcessingCost, @FinalGrossProfit,
                @DataSetId, @PreviousMonthQuantity, @PreviousMonthAmount
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
                ProductCode = row.ProductCode ?? string.Empty,
                GradeCode = row.GradeCode ?? string.Empty,
                ClassCode = row.ClassCode ?? string.Empty,
                ShippingMarkCode = row.ShippingMarkCode ?? string.Empty,
                ShippingMarkName = row.ShippingMarkName ?? string.Empty
            },
            ProductName = row.ProductName ?? string.Empty,
            Unit = row.Unit ?? string.Empty,
            StandardPrice = row.StandardPrice ?? 0m,
            ProductCategory1 = row.ProductCategory1 ?? string.Empty,
            ProductCategory2 = row.ProductCategory2 ?? string.Empty,
            JobDate = row.JobDate ?? DateTime.MinValue,
            CreatedDate = row.CreatedDate ?? DateTime.MinValue,
            UpdatedDate = row.UpdatedDate ?? DateTime.MinValue,
            CurrentStock = row.CurrentStock ?? 0m,
            CurrentStockAmount = row.CurrentStockAmount ?? 0m,
            DailyStock = row.DailyStock ?? 0m,
            DailyStockAmount = row.DailyStockAmount ?? 0m,
            DailyFlag = ConvertToChar(row.DailyFlag),
            DailyGrossProfit = row.DailyGrossProfit ?? 0m,
            DailyAdjustmentAmount = row.DailyAdjustmentAmount ?? 0m,
            DailyProcessingCost = row.DailyProcessingCost ?? 0m,
            FinalGrossProfit = row.FinalGrossProfit ?? 0m,
            DataSetId = row.DataSetId ?? string.Empty,
            PreviousMonthQuantity = row.PreviousMonthQuantity ?? 0m,
            PreviousMonthAmount = row.PreviousMonthAmount ?? 0m
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
            inventory.DataSetId,
            inventory.PreviousMonthQuantity,
            inventory.PreviousMonthAmount
        };
    }
    
    /// <summary>
    /// 動的な値をchar型に変換する
    /// </summary>
    /// <param name="value">変換対象の値</param>
    /// <param name="defaultValue">デフォルト値（値がnullまたは空の場合）</param>
    /// <returns>変換されたchar値</returns>
    private static char ConvertToChar(dynamic value, char defaultValue = '9')
    {
        if (value == null)
            return defaultValue;
        
        // 文字列の場合
        if (value is string strValue)
        {
            return string.IsNullOrEmpty(strValue) ? defaultValue : strValue[0];
        }
        
        // 既にchar型の場合
        if (value is char charValue)
        {
            return charValue;
        }
        
        // その他の型の場合は文字列に変換して最初の文字を取得
        try
        {
            var converted = value.ToString();
            return string.IsNullOrEmpty(converted) ? defaultValue : converted[0];
        }
        catch
        {
            return defaultValue;
        }
    }
    
    public async Task<int> UpdateJobDateForVouchersAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE im
            SET im.JobDate = @JobDate,
                im.UpdatedDate = GETDATE()
            FROM InventoryMaster im
            WHERE EXISTS (
                SELECT 1 FROM (
                    SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                    FROM SalesVouchers WHERE JobDate = @JobDate
                    UNION
                    SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                    FROM PurchaseVouchers WHERE JobDate = @JobDate
                ) v
                WHERE v.ProductCode = im.ProductCode
                    AND v.GradeCode = im.GradeCode
                    AND v.ClassCode = im.ClassCode
                    AND v.ShippingMarkCode = im.ShippingMarkCode
                    AND v.ShippingMarkName COLLATE Japanese_CI_AS = im.ShippingMarkName COLLATE Japanese_CI_AS
            )";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            
            LogInfo($"Updated JobDate for {result} inventory records", new { jobDate });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateJobDateForVouchersAsync), new { jobDate });
            throw;
        }
    }
    
    public async Task<int> RegisterNewProductsAsync(DateTime jobDate)
    {
        const string sql = @"
            INSERT INTO InventoryMaster (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                DailyFlag, CreatedDate, UpdatedDate, DataSetId,
                PreviousMonthQuantity, PreviousMonthAmount
            )
            SELECT DISTINCT
                combined.ProductCode, 
                combined.GradeCode, 
                combined.ClassCode, 
                combined.ShippingMarkCode, 
                combined.ShippingMarkName COLLATE Japanese_CI_AS,
                COALESCE(pm.ProductName, '商' + combined.ProductCode) AS ProductName,
                COALESCE(pm.UnitCode, '個') AS Unit,
                COALESCE(pm.StandardPrice, 0.0000) AS StandardPrice,
                COALESCE(pm.ProductCategory1, '') AS ProductCategory1,
                COALESCE(pm.ProductCategory2, '') AS ProductCategory2,
                @JobDate, 
                ISNULL(pmi.Quantity, 0.0000), -- 前月末在庫から初期在庫を設定
                ISNULL(pmi.Amount, 0.0000),   -- 前月末在庫金額から初期在庫金額を設定
                ISNULL(pmi.Quantity, 0.0000), -- DailyStockも同じ値で初期化
                ISNULL(pmi.Amount, 0.0000),   -- DailyStockAmountも同じ値で初期化
                '9', 
                GETDATE(), 
                GETDATE(), 
                '',
                ISNULL(pmi.Quantity, 0.0000), -- 前月末在庫数量
                ISNULL(pmi.Amount, 0.0000)    -- 前月末在庫金額
            FROM (
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                FROM SalesVouchers 
                WHERE JobDate = @JobDate
                    AND (VoucherType = '51' OR VoucherType = '52')
                    AND (DetailType = '1' OR DetailType = '2')
                    AND Quantity != 0
                UNION
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                FROM PurchaseVouchers 
                WHERE JobDate = @JobDate
                    AND (VoucherType = '11' OR VoucherType = '12')
                    AND (DetailType = '1' OR DetailType = '2')
                    AND Quantity != 0
            ) AS combined
            LEFT JOIN ProductMaster pm ON pm.ProductCode = combined.ProductCode
            LEFT JOIN PreviousMonthInventory pmi ON 
                pmi.ProductCode = combined.ProductCode
                AND pmi.GradeCode = combined.GradeCode
                AND pmi.ClassCode = combined.ClassCode
                AND pmi.ShippingMarkCode = combined.ShippingMarkCode
                AND pmi.ShippingMarkName COLLATE Japanese_CI_AS = combined.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE NOT EXISTS (
                SELECT 1 FROM InventoryMaster im
                WHERE im.ProductCode = combined.ProductCode
                    AND im.GradeCode = combined.GradeCode
                    AND im.ClassCode = combined.ClassCode
                    AND im.ShippingMarkCode = combined.ShippingMarkCode
                    AND im.ShippingMarkName COLLATE Japanese_CI_AS = combined.ShippingMarkName COLLATE Japanese_CI_AS
                    AND im.JobDate = @JobDate
            )";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            
            LogInfo($"Registered {result} new products to inventory", new { jobDate });
            
            // 登録した商品の詳細をログ出力
            if (result > 0)
            {
                const string detailSql = @"
                    SELECT TOP 10 ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, ProductName
                    FROM InventoryMaster
                    WHERE JobDate = @JobDate AND CreatedDate >= DATEADD(MINUTE, -1, GETDATE())
                    ORDER BY CreatedDate DESC";
                
                var newProducts = await connection.QueryAsync<dynamic>(detailSql, new { JobDate = jobDate });
                foreach (var product in newProducts)
                {
                    LogDebug($"New product registered: ProductCode={product.ProductCode}, ProductName={product.ProductName}, ShippingMarkName={product.ShippingMarkName}");
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(RegisterNewProductsAsync), new { jobDate });
            throw;
        }
    }
    
    public async Task<int> UpdateFromCpInventoryAsync(string dataSetId, DateTime jobDate)
    {
        const string sql = @"
            UPDATE im
            SET im.CurrentStock = cp.DailyStock,
                im.CurrentStockAmount = cp.DailyStockAmount,
                im.DailyStock = cp.DailyStock,
                im.DailyStockAmount = cp.DailyStockAmount,
                im.DailyFlag = cp.DailyFlag,
                im.UpdatedDate = GETDATE(),
                im.DataSetId = cp.DataSetId
            FROM InventoryMaster im
            INNER JOIN CpInventoryMaster cp ON
                im.ProductCode = cp.ProductCode
                AND im.GradeCode = cp.GradeCode
                AND im.ClassCode = cp.ClassCode
                AND im.ShippingMarkCode = cp.ShippingMarkCode
                AND im.ShippingMarkName COLLATE Japanese_CI_AS = cp.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId
                AND im.JobDate = @JobDate";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, JobDate = jobDate });
            
            LogInfo($"Updated inventory from CP inventory for {result} records", new { dataSetId, jobDate });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateFromCpInventoryAsync), new { dataSetId, jobDate });
            throw;
        }
    }

    /// <summary>
    /// 在庫マスタから任意の日付で商品キーに一致するレコードを取得（最新日付優先）
    /// </summary>
    public async Task<InventoryMaster?> GetByKeyAnyDateAsync(InventoryKey key)
    {
        const string sql = @"
            SELECT TOP 1 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                DailyFlag, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
                DataSetId, PreviousMonthQuantity, PreviousMonthAmount
            FROM InventoryMaster 
            WHERE ProductCode = @ProductCode 
                AND GradeCode = @GradeCode 
                AND ClassCode = @ClassCode 
                AND ShippingMarkCode = @ShippingMarkCode 
                AND ShippingMarkName COLLATE Japanese_CI_AS = @ShippingMarkName COLLATE Japanese_CI_AS
            ORDER BY JobDate DESC";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new
            {
                ProductCode = key.ProductCode,
                GradeCode = key.GradeCode,
                ClassCode = key.ClassCode,
                ShippingMarkCode = key.ShippingMarkCode,
                ShippingMarkName = key.ShippingMarkName
            });

            if (result != null)
            {
                LogDebug($"Found inventory master for product key", new { key });
                return MapToInventoryMaster(result);
            }

            LogDebug($"No inventory master found for product key", new { key });
            return null;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByKeyAnyDateAsync), new { key });
            throw;
        }
    }
    
    /// <summary>
    /// 売上・仕入・在庫調整から在庫マスタの初期データを作成
    /// </summary>
    public async Task<int> CreateInitialInventoryFromVouchersAsync(DateTime jobDate)
    {
        const string sql = @"
            INSERT INTO InventoryMaster (
                ProductCode, ProductName, GradeCode, ClassCode, 
                ShippingMarkCode, ShippingMarkName, 
                Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CurrentStock, CurrentStockAmount, 
                DailyStock, DailyStockAmount, DailyFlag, DataSetId,
                CreatedDate, UpdatedDate
            )
            SELECT DISTINCT
                sv.ProductCode,
                ISNULL(sv.ProductName, '商品名未設定'),
                sv.GradeCode,
                sv.ClassCode,
                sv.ShippingMarkCode,
                sv.ShippingMarkName,
                '個',  -- デフォルト単位
                0,     -- 標準単価（後で商品マスタから更新）
                '',    -- 商品分類1（後で商品マスタから更新）
                '',    -- 商品分類2（後で商品マスタから更新）
                sv.JobDate,
                0,     -- 現在在庫
                0,     -- 現在在庫金額
                0,     -- 当日在庫
                0,     -- 当日在庫金額
                '9',   -- 当日発生フラグ（未処理）
                '',    -- DataSetId
                GETDATE(),
                GETDATE()
            FROM (
                -- 売上伝票
                SELECT DISTINCT 
                    ProductCode, ProductName, GradeCode, ClassCode, 
                    ShippingMarkCode, ShippingMarkName, JobDate
                FROM SalesVouchers
                WHERE JobDate = @JobDate
                
                UNION
                
                -- 仕入伝票
                SELECT DISTINCT 
                    ProductCode, ProductName, GradeCode, ClassCode, 
                    ShippingMarkCode, ShippingMarkName, JobDate
                FROM PurchaseVouchers
                WHERE JobDate = @JobDate
                
                UNION
                
                -- 在庫調整
                SELECT DISTINCT 
                    ProductCode, ProductName, GradeCode, ClassCode, 
                    ShippingMarkCode, ShippingMarkName, JobDate
                FROM InventoryAdjustments
                WHERE JobDate = @JobDate
            ) sv
            WHERE NOT EXISTS (
                SELECT 1 FROM InventoryMaster im
                WHERE im.ProductCode = sv.ProductCode
                    AND im.GradeCode = sv.GradeCode
                    AND im.ClassCode = sv.ClassCode
                    AND im.ShippingMarkCode = sv.ShippingMarkCode
                    AND im.ShippingMarkName = sv.ShippingMarkName
                    AND im.JobDate = sv.JobDate
            );";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            
            LogInfo($"Created initial inventory master records: {result} items", new { jobDate });
            
            // 作成された在庫マスタの件数を確認
            if (result > 0)
            {
                const string countSql = @"
                    SELECT COUNT(DISTINCT ProductCode + '_' + GradeCode + '_' + ClassCode + '_' + ShippingMarkCode + '_' + ShippingMarkName)
                    FROM InventoryMaster
                    WHERE JobDate = @JobDate";
                
                var totalCount = await connection.QuerySingleAsync<int>(countSql, new { JobDate = jobDate });
                LogInfo($"Total inventory master records for JobDate {jobDate:yyyy-MM-dd}: {totalCount}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(CreateInitialInventoryFromVouchersAsync), new { jobDate });
            throw;
        }
    }
}