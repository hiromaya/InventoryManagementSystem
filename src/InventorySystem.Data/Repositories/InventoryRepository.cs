using System.Data;
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
                DataSetId, PreviousMonthQuantity, PreviousMonthAmount,
                IsActive, ParentDataSetId, ImportType, CreatedBy, CreatedAt, UpdatedAt
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
            PreviousMonthAmount = row.PreviousMonthAmount ?? 0m,
            // 新規フィールド
            IsActive = row.IsActive ?? true,
            ParentDataSetId = row.ParentDataSetId,
            ImportType = row.ImportType ?? "UNKNOWN",
            CreatedBy = row.CreatedBy,
            CreatedAt = row.CreatedAt ?? DateTime.Now,
            UpdatedAt = row.UpdatedAt
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
            inventory.PreviousMonthAmount,
            // 新規フィールド
            inventory.IsActive,
            inventory.ParentDataSetId,
            inventory.ImportType,
            inventory.CreatedBy,
            inventory.CreatedAt,
            inventory.UpdatedAt
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
            LEFT JOIN (
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                       PreviousMonthQuantity AS Quantity, PreviousMonthAmount AS Amount
                FROM InventoryMaster
                WHERE PreviousMonthQuantity IS NOT NULL OR PreviousMonthAmount IS NOT NULL
            ) pmi ON 
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
                im.DailyGrossProfit = cp.DailyGrossProfit,
                im.FinalGrossProfit = cp.DailyGrossProfit,
                im.UpdatedDate = GETDATE(),
                im.DataSetId = cp.DataSetId,
                im.JobDate = @JobDate  -- 最終処理日として更新
            FROM InventoryMaster im
            INNER JOIN CpInventoryMaster cp ON
                im.ProductCode = cp.ProductCode
                AND im.GradeCode = cp.GradeCode
                AND im.ClassCode = cp.ClassCode
                AND im.ShippingMarkCode = cp.ShippingMarkCode
                AND im.ShippingMarkName COLLATE Japanese_CI_AS = cp.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, JobDate = jobDate });
            
            LogInfo($"Updated inventory from CP inventory for {result} records (cumulative mode)", new { dataSetId, jobDate });
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
    
    public async Task<int> GetCountByJobDateAsync(DateTime jobDate)
    {
        const string sql = "SELECT COUNT(*) FROM InventoryMaster WHERE JobDate = @JobDate";
        
        try
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, new { JobDate = jobDate });
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetCountByJobDateAsync), new { jobDate });
            throw;
        }
    }
    
    /// <summary>
    /// アクティブなデータのみ取得
    /// </summary>
    public async Task<List<InventoryMaster>> GetActiveByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                DailyFlag, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
                DataSetId, PreviousMonthQuantity, PreviousMonthAmount,
                IsActive, ParentDataSetId, ImportType, CreatedBy, CreatedAt, UpdatedAt
            FROM InventoryMaster 
            WHERE JobDate = @JobDate AND IsActive = 1
            ORDER BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName";
    
        try
        {
            using var connection = CreateConnection();
            var inventories = await connection.QueryAsync<dynamic>(sql, new { JobDate = jobDate });
            return inventories.Select(MapToInventoryMaster).ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetActiveByJobDateAsync), new { jobDate });
            throw;
        }
    }
    
    /// <summary>
    /// データセットを無効化（削除しない）
    /// </summary>
    public async Task DeactivateDataSetAsync(string dataSetId)
    {
        const string sql = @"
            UPDATE InventoryMaster 
            SET IsActive = 0, UpdatedAt = GETDATE()
            WHERE DataSetId = @DataSetId";
    
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });
            LogInfo($"Deactivated {result} records for DataSetId: {dataSetId}");
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeactivateDataSetAsync), new { dataSetId });
            throw;
        }
    }
    
    /// <summary>
    /// JobDateのデータを無効化
    /// </summary>
    public async Task DeactivateByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE InventoryMaster 
            SET IsActive = 0, UpdatedAt = GETDATE()
            WHERE JobDate = @JobDate AND IsActive = 1";
    
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            LogInfo($"Deactivated {result} records for JobDate: {jobDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeactivateByJobDateAsync), new { jobDate });
            throw;
        }
    }
    
    /// <summary>
    /// 前月末在庫（INIT）の取得
    /// </summary>
    public async Task<List<InventoryMaster>> GetActiveInitInventoryAsync(DateTime lastMonthEnd)
    {
        const string sql = @"
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                DailyFlag, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
                DataSetId, PreviousMonthQuantity, PreviousMonthAmount,
                IsActive, ParentDataSetId, ImportType, CreatedBy, CreatedAt, UpdatedAt
            FROM InventoryMaster 
            WHERE JobDate = @JobDate 
            AND ImportType = 'INIT'
            AND IsActive = 1";
    
        try
        {
            using var connection = CreateConnection();
            var inventories = await connection.QueryAsync<dynamic>(sql, new { JobDate = lastMonthEnd });
            return inventories.Select(MapToInventoryMaster).ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetActiveInitInventoryAsync), new { lastMonthEnd });
            throw;
        }
    }
    
    /// <summary>
    /// 伝票データから在庫マスタを累積更新（既存レコードは更新、新規は追加）
    /// </summary>
    public async Task<int> UpdateOrCreateFromVouchersAsync(DateTime jobDate, string datasetId)
    {
        using var connection = CreateConnection();
        
        try
        {
            var result = await connection.QuerySingleAsync<dynamic>(
                "sp_UpdateOrCreateInventoryMasterCumulative",
                new { JobDate = jobDate, DatasetId = datasetId },
                commandType: CommandType.StoredProcedure
            );
            
            var totalCount = (result?.InsertedCount ?? 0) + (result?.UpdatedCount ?? 0);
            
            LogInfo($"在庫マスタ累積更新完了: 新規={result?.InsertedCount ?? 0}, 更新={result?.UpdatedCount ?? 0}", 
                new { jobDate, datasetId });
            
            return totalCount;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateOrCreateFromVouchersAsync), new { jobDate, datasetId });
            throw;
        }
    }
    
    /// <summary>
    /// 重複レコードのクリーンアップ（一時的な修正処理）
    /// </summary>
    public async Task<int> CleanupDuplicateRecordsAsync()
    {
        const string sql = @"
            WITH DuplicateRecords AS (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    JobDate,
                    ROW_NUMBER() OVER (
                        PARTITION BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName 
                        ORDER BY JobDate DESC, UpdatedDate DESC
                    ) as rn
                FROM InventoryMaster
            )
            DELETE FROM InventoryMaster
            WHERE EXISTS (
                SELECT 1 
                FROM DuplicateRecords dr
                WHERE dr.ProductCode = InventoryMaster.ProductCode
                  AND dr.GradeCode = InventoryMaster.GradeCode
                  AND dr.ClassCode = InventoryMaster.ClassCode
                  AND dr.ShippingMarkCode = InventoryMaster.ShippingMarkCode
                  AND dr.ShippingMarkName = InventoryMaster.ShippingMarkName
                  AND dr.JobDate = InventoryMaster.JobDate
                  AND dr.rn > 1
            );
            
            SELECT @@ROWCOUNT;";
        
        using var connection = CreateConnection();
        var deletedCount = await connection.ExecuteScalarAsync<int>(sql);
        
        LogInfo($"重複レコードを削除しました: {deletedCount}件");
        return deletedCount;
    }
    
    /// <summary>
    /// 月初に前月末在庫からCurrentStockを初期化
    /// </summary>
    public async Task<int> InitializeMonthlyInventoryAsync(string yearMonth)
    {
        const string sql = @"
            -- 指定月の初日を計算
            DECLARE @FirstDayOfMonth DATE = CAST(@YearMonth + '01' AS DATE);
            
            -- 前月末在庫データから現在庫を初期化
            UPDATE im
            SET im.CurrentStock = im.PreviousMonthQuantity,
                im.CurrentStockAmount = im.PreviousMonthAmount,
                im.DailyStock = im.PreviousMonthQuantity,
                im.DailyStockAmount = im.PreviousMonthAmount,
                im.UpdatedDate = GETDATE(),
                im.JobDate = @FirstDayOfMonth
            FROM InventoryMaster im
            WHERE im.PreviousMonthQuantity IS NOT NULL
                OR im.PreviousMonthAmount IS NOT NULL;
            
            SELECT @@ROWCOUNT;";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteScalarAsync<int>(sql, new { YearMonth = yearMonth });
            
            LogInfo($"Initialized {result} inventory records for month {yearMonth}");
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(InitializeMonthlyInventoryAsync), new { yearMonth });
            throw;
        }
    }
    
    /// <summary>
    /// 指定されたキーで最新の在庫マスタを取得（全期間対象）
    /// </summary>
    public async Task<InventoryMaster?> GetLatestByKeyAsync(InventoryKey key)
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
                -- ShippingMarkNameは部分一致で検索（空白8文字の場合を考慮）
                AND RTRIM(ShippingMarkName) = RTRIM(@ShippingMarkName)
            ORDER BY JobDate DESC, UpdatedDate DESC";

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
                LogDebug($"Found latest inventory master for product key", new { key, JobDate = result.JobDate });
                return MapToInventoryMaster(result);
            }

            LogDebug($"No inventory master found for product key", new { key });
            return null;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetLatestByKeyAsync), new { key });
            throw;
        }
    }
    
    /// <summary>
    /// 最新のINIT（前月末在庫）データを取得
    /// </summary>
    public async Task<List<InventoryMaster>> GetLatestInitInventoryAsync()
    {
        const string sql = @"
            SELECT * FROM InventoryMaster
            WHERE ImportType = 'INIT'
              AND IsActive = 1
            ORDER BY CreatedDate DESC";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<dynamic>(sql);
            return result.Select(MapToInventoryMaster).ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetLatestInitInventoryAsync));
            throw;
        }
    }

    /// <summary>
    /// 最新の有効な在庫データを取得（日付に関係なく）
    /// </summary>
    public async Task<List<InventoryMaster>> GetLatestActiveInventoryAsync()
    {
        const string sql = @"
            WITH LatestInventory AS (
                SELECT 
                    DataSetId,
                    MAX(CreatedDate) as LatestDate
                FROM InventoryMaster
                WHERE IsActive = 1
                GROUP BY DataSetId
            )
            SELECT im.* 
            FROM InventoryMaster im
            INNER JOIN LatestInventory li 
                ON im.DataSetId = li.DataSetId 
                AND im.CreatedDate = li.LatestDate
            WHERE im.IsActive = 1";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<dynamic>(sql);
            return result.Select(MapToInventoryMaster).ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetLatestActiveInventoryAsync));
            throw;
        }
    }
    
    /// <summary>
    /// 最終処理日（最新のJobDate）を取得
    /// </summary>
    public async Task<DateTime> GetMaxJobDateAsync()
    {
        const string sql = @"
            SELECT ISNULL(MAX(JobDate), '2025-05-31') as MaxJobDate
            FROM InventoryMaster
            WHERE IsActive = 1";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.QuerySingleAsync<DateTime>(sql);
            LogInfo($"最終処理日を取得しました: {result:yyyy-MM-dd}");
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetMaxJobDateAsync));
            throw;
        }
    }
    
    /// <summary>
    /// 全有効在庫データを取得（日付関係なく最新の状態）
    /// </summary>
    public async Task<List<InventoryMaster>> GetAllActiveInventoryAsync()
    {
        const string sql = @"
            SELECT * FROM InventoryMaster 
            WHERE IsActive = 1
            ORDER BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<dynamic>(sql);
            var inventories = result.Select(MapToInventoryMaster).ToList();
            LogInfo($"全有効在庫データを取得しました: {inventories.Count}件");
            return inventories;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetAllActiveInventoryAsync));
            throw;
        }
    }
    
    /// <summary>
    /// 在庫データのMERGE処理（既存は更新、新規は挿入）
    /// </summary>
    public async Task<int> MergeInventoryAsync(List<InventoryMaster> inventories, DateTime targetDate, string dataSetId)
    {
        const string sql = @"
            MERGE InventoryMaster AS target
            USING (
                SELECT 
                    @ProductCode as ProductCode,
                    @GradeCode as GradeCode,
                    @ClassCode as ClassCode,
                    @ShippingMarkCode as ShippingMarkCode,
                    @ShippingMarkName as ShippingMarkName,
                    @ProductName as ProductName,
                    @Unit as Unit,
                    @StandardPrice as StandardPrice,
                    @CurrentStock as CurrentStock,
                    @CurrentStockAmount as CurrentStockAmount,
                    @DailyStock as DailyStock,
                    @DailyStockAmount as DailyStockAmount,
                    @PreviousMonthQuantity as PreviousMonthQuantity,
                    @PreviousMonthAmount as PreviousMonthAmount
            ) AS source
            ON (
                target.ProductCode = source.ProductCode AND
                target.GradeCode = source.GradeCode AND
                target.ClassCode = source.ClassCode AND
                target.ShippingMarkCode = source.ShippingMarkCode AND
                target.ShippingMarkName COLLATE Japanese_CI_AS = source.ShippingMarkName COLLATE Japanese_CI_AS
            )
            WHEN MATCHED THEN
                UPDATE SET 
                    ProductName = source.ProductName,
                    Unit = source.Unit,
                    StandardPrice = source.StandardPrice,
                    CurrentStock = source.CurrentStock,
                    CurrentStockAmount = source.CurrentStockAmount,
                    DailyStock = source.DailyStock,
                    DailyStockAmount = source.DailyStockAmount,
                    PreviousMonthQuantity = source.PreviousMonthQuantity,
                    PreviousMonthAmount = source.PreviousMonthAmount,
                    JobDate = @TargetDate,
                    DataSetId = @DataSetId,
                    UpdatedDate = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    ProductName, Unit, StandardPrice,
                    CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                    PreviousMonthQuantity, PreviousMonthAmount,
                    JobDate, DataSetId, IsActive, CreatedDate, UpdatedDate
                )
                VALUES (
                    source.ProductCode, source.GradeCode, source.ClassCode, 
                    source.ShippingMarkCode, source.ShippingMarkName,
                    source.ProductName, source.Unit, source.StandardPrice,
                    source.CurrentStock, source.CurrentStockAmount, 
                    source.DailyStock, source.DailyStockAmount,
                    source.PreviousMonthQuantity, source.PreviousMonthAmount,
                    @TargetDate, @DataSetId, 1, GETDATE(), GETDATE()
                );";
        
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            
            var totalAffected = 0;
            
            foreach (var inventory in inventories)
            {
                var parameters = new
                {
                    ProductCode = inventory.Key.ProductCode,
                    GradeCode = inventory.Key.GradeCode,
                    ClassCode = inventory.Key.ClassCode,
                    ShippingMarkCode = inventory.Key.ShippingMarkCode,
                    ShippingMarkName = inventory.Key.ShippingMarkName,
                    inventory.ProductName,
                    inventory.Unit,
                    inventory.StandardPrice,
                    inventory.CurrentStock,
                    inventory.CurrentStockAmount,
                    inventory.DailyStock,
                    inventory.DailyStockAmount,
                    inventory.PreviousMonthQuantity,
                    inventory.PreviousMonthAmount,
                    TargetDate = targetDate,
                    DataSetId = dataSetId
                };
                
                totalAffected += await connection.ExecuteAsync(sql, parameters, transaction);
            }
            
            await transaction.CommitAsync();
            LogInfo($"在庫マスタMERGE処理完了: {totalAffected}件", new { targetDate, dataSetId });
            return totalAffected;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(MergeInventoryAsync), new { targetDate, dataSetId, count = inventories.Count });
            throw;
        }
    }
    
    /// <summary>
    /// ImportTypeで在庫データを取得
    /// </summary>
    public async Task<IEnumerable<InventoryMaster>> GetByImportTypeAsync(string importType)
    {
        const string sql = @"
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                DailyFlag, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
                DataSetId, PreviousMonthQuantity, PreviousMonthAmount,
                IsActive, ParentDataSetId, ImportType, CreatedBy, CreatedAt, UpdatedAt
            FROM InventoryMaster 
            WHERE ImportType = @ImportType 
            AND IsActive = 1
            ORDER BY JobDate DESC, CreatedDate DESC";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<dynamic>(sql, new { ImportType = importType });
            
            LogInfo($"ImportType '{importType}' で {result.Count()} 件の在庫データを取得");
            return result.Select(MapToInventoryMaster).ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByImportTypeAsync), new { importType });
            throw;
        }
    }
    
    /// <summary>
    /// ImportTypeで在庫データを無効化
    /// </summary>
    public async Task<int> DeactivateByImportTypeAsync(string importType)
    {
        const string sql = @"
            UPDATE InventoryMaster 
            SET IsActive = 0,
                UpdatedDate = GETDATE()
            WHERE ImportType = @ImportType 
            AND IsActive = 1";
        
        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, new { ImportType = importType });
            
            LogInfo($"ImportType '{importType}' の {count} 件の在庫データを無効化");
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeactivateByImportTypeAsync), new { importType });
            throw;
        }
    }
    
    /// <summary>
    /// トランザクション内で初期在庫データを一括処理
    /// </summary>
    public async Task<int> ProcessInitialInventoryInTransactionAsync(
        List<InventoryMaster> inventories, 
        DataSetManagement dataSetManagement,
        bool deactivateExisting = true)
    {
        if (inventories == null || !inventories.Any())
        {
            LogWarning("処理対象の在庫データがありません");
            return 0;
        }
        
        return await ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var totalProcessed = 0;
            
            try
            {
                // 1. 既存のINITデータを無効化
                if (deactivateExisting)
                {
                    const string deactivateSql = @"
                        UPDATE InventoryMaster 
                        SET IsActive = 0,
                            UpdatedDate = GETDATE()
                        WHERE ImportType = 'INIT' 
                        AND IsActive = 1";
                    
                    var deactivatedCount = await connection.ExecuteAsync(deactivateSql, transaction: transaction);
                    LogInfo($"既存のINITデータ {deactivatedCount} 件を無効化しました");
                }
                
                // 2. 新規在庫データの一括登録
                // 既存データの更新と新規データの挿入を分ける
                var updateSql = @"
                    UPDATE InventoryMaster SET
                        PreviousMonthQuantity = @PreviousMonthQuantity,
                        PreviousMonthAmount = @PreviousMonthAmount,
                        CurrentStock = @CurrentStock,
                        CurrentStockAmount = @CurrentStockAmount,
                        DailyStock = @DailyStock,
                        DailyStockAmount = @DailyStockAmount,
                        JobDate = @JobDate,
                        UpdatedDate = GETDATE(),
                        DataSetId = @DataSetId,
                        ImportType = @ImportType,
                        IsActive = @IsActive,
                        ProductName = @ProductName
                    WHERE ProductCode = @ProductCode
                      AND GradeCode = @GradeCode
                      AND ClassCode = @ClassCode
                      AND ShippingMarkCode = @ShippingMarkCode
                      AND ShippingMarkName = @ShippingMarkName";
                
                var insertSql = @"
                    INSERT INTO InventoryMaster (
                        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                        ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                        JobDate, CreatedDate, UpdatedDate,
                        CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                        DailyFlag, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost,
                        FinalGrossProfit, DataSetId, PreviousMonthQuantity, PreviousMonthAmount,
                        IsActive, ImportType, CreatedBy
                    ) VALUES (
                        @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                        @ProductName, @Unit, @StandardPrice, @ProductCategory1, @ProductCategory2,
                        @JobDate, @CreatedDate, @UpdatedDate,
                        @CurrentStock, @CurrentStockAmount, @DailyStock, @DailyStockAmount,
                        @DailyFlag, @DailyGrossProfit, @DailyAdjustmentAmount, @DailyProcessingCost,
                        @FinalGrossProfit, @DataSetId, @PreviousMonthQuantity, @PreviousMonthAmount,
                        @IsActive, @ImportType, @CreatedBy
                    )";
                
                // バッチ処理で効率化（1000件ずつ）
                const int batchSize = 1000;
                for (int i = 0; i < inventories.Count; i += batchSize)
                {
                    var batch = inventories.Skip(i).Take(batchSize).ToList();
                    var parameters = batch.Select(inv => new
                    {
                        ProductCode = inv.Key.ProductCode,
                        GradeCode = inv.Key.GradeCode,
                        ClassCode = inv.Key.ClassCode,
                        ShippingMarkCode = inv.Key.ShippingMarkCode,
                        ShippingMarkName = inv.Key.ShippingMarkName,
                        inv.ProductName,
                        inv.Unit,
                        inv.StandardPrice,
                        inv.ProductCategory1,
                        inv.ProductCategory2,
                        inv.JobDate,
                        inv.CreatedDate,
                        inv.UpdatedDate,
                        inv.CurrentStock,
                        inv.CurrentStockAmount,
                        inv.DailyStock,
                        inv.DailyStockAmount,
                        inv.DailyFlag,
                        inv.DailyGrossProfit,
                        inv.DailyAdjustmentAmount,
                        inv.DailyProcessingCost,
                        inv.FinalGrossProfit,
                        inv.DataSetId,
                        inv.PreviousMonthQuantity,
                        inv.PreviousMonthAmount,
                        inv.IsActive,
                        inv.ImportType,
                        inv.CreatedBy
                    }).ToList();
                    
                    // まず更新を試み、更新されなかった場合は挿入
                    foreach (var param in parameters)
                    {
                        var updated = await connection.ExecuteAsync(updateSql, param, transaction);
                        if (updated == 0)
                        {
                            await connection.ExecuteAsync(insertSql, param, transaction);
                        }
                        totalProcessed++;
                    }
                }
                
                LogInfo($"在庫データ {totalProcessed} 件を処理しました");
                
                // DataSetManagement処理はUnifiedDataSetServiceで実行済み
                // 責任分離の原則に従い、InventoryRepositoryは在庫データのCRUDのみを担当
                
                return totalProcessed;
            }
            catch (Exception ex)
            {
                LogError(ex, "トランザクション内でエラーが発生しました", new { 
                    InventoryCount = inventories.Count,
                    DatasetId = dataSetManagement?.DataSetId ?? "UnifiedDataSetService管理"
                });
                throw;
            }
        });
    }
    
    /// <summary>
    /// トランザクション内で在庫引継ぎ処理を実行
    /// </summary>
    public async Task<int> ProcessCarryoverInTransactionAsync(
        List<InventoryMaster> inventories,
        DateTime targetDate,
        string dataSetId,
        DataSetManagement dataSetManagement)
    {
        if (inventories == null || !inventories.Any())
        {
            LogWarning("処理対象の在庫データがありません");
            return 0;
        }
        
        return await ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            try
            {
                // 1. MERGE処理で在庫データを更新
                var mergeSql = @"
                    MERGE InventoryMaster AS target
                    USING (
                        SELECT 
                            @ProductCode as ProductCode,
                            @GradeCode as GradeCode,
                            @ClassCode as ClassCode,
                            @ShippingMarkCode as ShippingMarkCode,
                            @ShippingMarkName as ShippingMarkName,
                            @ProductName as ProductName,
                            @Unit as Unit,
                            @StandardPrice as StandardPrice,
                            @CurrentStock as CurrentStock,
                            @CurrentStockAmount as CurrentStockAmount,
                            @DailyStock as DailyStock,
                            @DailyStockAmount as DailyStockAmount,
                            @PreviousMonthQuantity as PreviousMonthQuantity,
                            @PreviousMonthAmount as PreviousMonthAmount
                    ) AS source
                    ON (
                        target.ProductCode = source.ProductCode AND
                        target.GradeCode = source.GradeCode AND
                        target.ClassCode = source.ClassCode AND
                        target.ShippingMarkCode = source.ShippingMarkCode AND
                        target.ShippingMarkName COLLATE Japanese_CI_AS = source.ShippingMarkName COLLATE Japanese_CI_AS
                    )
                    WHEN MATCHED THEN
                        UPDATE SET 
                            ProductName = source.ProductName,
                            Unit = source.Unit,
                            StandardPrice = source.StandardPrice,
                            CurrentStock = source.CurrentStock,
                            CurrentStockAmount = source.CurrentStockAmount,
                            DailyStock = source.DailyStock,
                            DailyStockAmount = source.DailyStockAmount,
                            PreviousMonthQuantity = source.PreviousMonthQuantity,
                            PreviousMonthAmount = source.PreviousMonthAmount,
                            JobDate = @TargetDate,
                            DataSetId = @DataSetId,
                            UpdatedDate = GETDATE(),
                            ImportType = 'CARRYOVER',
                            IsActive = 1
                    WHEN NOT MATCHED THEN
                        INSERT (
                            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                            ProductName, Unit, StandardPrice,
                            CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
                            PreviousMonthQuantity, PreviousMonthAmount,
                            JobDate, DataSetId, IsActive, ImportType, CreatedDate, UpdatedDate
                        )
                        VALUES (
                            source.ProductCode, source.GradeCode, source.ClassCode, 
                            source.ShippingMarkCode, source.ShippingMarkName,
                            source.ProductName, source.Unit, source.StandardPrice,
                            source.CurrentStock, source.CurrentStockAmount, 
                            source.DailyStock, source.DailyStockAmount,
                            source.PreviousMonthQuantity, source.PreviousMonthAmount,
                            @TargetDate, @DataSetId, 1, 'CARRYOVER', GETDATE(), GETDATE()
                        );";
                
                var totalAffected = 0;
                
                // バッチ処理で効率化
                const int batchSize = 1000;
                for (int i = 0; i < inventories.Count; i += batchSize)
                {
                    var batch = inventories.Skip(i).Take(batchSize).ToList();
                    
                    foreach (var inventory in batch)
                    {
                        var parameters = new
                        {
                            ProductCode = inventory.Key.ProductCode,
                            GradeCode = inventory.Key.GradeCode,
                            ClassCode = inventory.Key.ClassCode,
                            ShippingMarkCode = inventory.Key.ShippingMarkCode,
                            ShippingMarkName = inventory.Key.ShippingMarkName,
                            inventory.ProductName,
                            inventory.Unit,
                            inventory.StandardPrice,
                            inventory.CurrentStock,
                            inventory.CurrentStockAmount,
                            inventory.DailyStock,
                            inventory.DailyStockAmount,
                            inventory.PreviousMonthQuantity,
                            inventory.PreviousMonthAmount,
                            TargetDate = targetDate,
                            DataSetId = dataSetId
                        };
                        
                        totalAffected += await connection.ExecuteAsync(mergeSql, parameters, transaction);
                    }
                }
                
                LogInfo($"在庫引継ぎMERGE処理: {totalAffected}件");
                
                // 2. DataSetManagementテーブルへの登録
                const string datasetSql = @"
                    INSERT INTO DataSetManagement (
                        DatasetId, JobDate, ProcessType, ImportType, RecordCount, TotalRecordCount,
                        IsActive, IsArchived, ParentDataSetId, ImportedFiles, CreatedAt, CreatedBy, 
                        Notes, Department
                    ) VALUES (
                        @DatasetId, @JobDate, @ProcessType, @ImportType, @RecordCount, @TotalRecordCount,
                        @IsActive, @IsArchived, @ParentDataSetId, @ImportedFiles, @CreatedAt, @CreatedBy, 
                        @Notes, @Department
                    )";
                
                await connection.ExecuteAsync(datasetSql, dataSetManagement, transaction);
                LogInfo($"DataSetManagement登録完了: DataSetId={dataSetManagement.DataSetId}");
                
                return totalAffected;
            }
            catch (Exception ex)
            {
                LogError(ex, "在庫引継ぎトランザクション処理でエラーが発生しました", new { 
                    InventoryCount = inventories.Count,
                    TargetDate = targetDate,
                    DatasetId = dataSetId 
                });
                throw;
            }
        });
    }
    
    /// <summary>
    /// 非アクティブ化対象の在庫件数を取得
    /// </summary>
    public async Task<int> GetInactiveTargetCountAsync(DateTime jobDate, int inactiveDays)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM InventoryMaster
            WHERE CurrentStock = 0
                AND ISNULL(PreviousMonthQuantity, 0) = 0
                AND IsActive = 1
                AND DATEDIFF(DAY, 
                    ISNULL(
                        CASE 
                            WHEN ISNULL(LastSalesDate, '1900-01-01') > ISNULL(LastPurchaseDate, '1900-01-01') 
                            THEN LastSalesDate
                            ELSE LastPurchaseDate
                        END,
                        JobDate  -- LastSalesDate/LastPurchaseDateが両方NULLの場合のみJobDateを使用
                    ), 
                    @JobDate) >= @InactiveDays";
        
        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(sql, new { JobDate = jobDate, InactiveDays = inactiveDays });
            
            LogInfo($"非アクティブ化対象件数: {count}件（基準: {inactiveDays}日以上更新なし）", 
                new { jobDate, inactiveDays });
            
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetInactiveTargetCountAsync), new { jobDate, inactiveDays });
            throw;
        }
    }
    
    /// <summary>
    /// 在庫ゼロの商品を非アクティブ化
    /// </summary>
    public async Task<int> DeactivateZeroStockItemsAsync(DateTime jobDate, int inactiveDays)
    {
        const string sql = @"
            UPDATE InventoryMaster
            SET IsActive = 0,
                UpdatedDate = GETDATE(),  -- これは単なる更新日時の記録
                Notes = CONCAT('Auto-deactivated on ', CONVERT(varchar, @JobDate, 23), 
                              ': Zero stock for ', @InactiveDays, '+ days since last transaction')
            WHERE CurrentStock = 0
                AND ISNULL(PreviousMonthQuantity, 0) = 0
                AND IsActive = 1
                AND DATEDIFF(DAY, 
                    ISNULL(
                        CASE 
                            WHEN ISNULL(LastSalesDate, '1900-01-01') > ISNULL(LastPurchaseDate, '1900-01-01') 
                            THEN LastSalesDate
                            ELSE LastPurchaseDate
                        END,
                        JobDate
                    ), 
                    @JobDate) >= @InactiveDays";
        
        try
        {
            using var connection = CreateConnection();
            var affected = await connection.ExecuteAsync(sql, new { JobDate = jobDate, InactiveDays = inactiveDays });
            
            LogInfo($"在庫ゼロ商品を非アクティブ化しました: {affected}件（基準: {inactiveDays}日以上更新なし）", 
                new { jobDate, inactiveDays, affected });
            
            return affected;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeactivateZeroStockItemsAsync), new { jobDate, inactiveDays });
            throw;
        }
    }
    
    /// <summary>
    /// 最終売上日を更新する
    /// </summary>
    public async Task UpdateLastSalesDateAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE im
            SET im.LastSalesDate = sv.JobDate,
                im.UpdatedDate = GETDATE()
            FROM InventoryMaster im
            INNER JOIN (
                SELECT DISTINCT 
                    ProductCode, GradeCode, ClassCode, 
                    ShippingMarkCode, ShippingMarkName,
                    MAX(JobDate) as JobDate
                FROM SalesVouchers
                WHERE JobDate = @JobDate
                GROUP BY ProductCode, GradeCode, ClassCode, 
                         ShippingMarkCode, ShippingMarkName
            ) sv ON 
                im.ProductCode = sv.ProductCode AND
                im.GradeCode = sv.GradeCode AND
                im.ClassCode = sv.ClassCode AND
                im.ShippingMarkCode = sv.ShippingMarkCode AND
                im.ShippingMarkName = sv.ShippingMarkName
            WHERE sv.JobDate > ISNULL(im.LastSalesDate, '1900-01-01')";
        
        try
        {
            using var connection = CreateConnection();
            var affected = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            LogInfo($"最終売上日を更新しました: {affected}件", new { jobDate, affected });
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateLastSalesDateAsync), new { jobDate });
            throw;
        }
    }
    
    /// <summary>
    /// 最終仕入日を更新する
    /// </summary>
    public async Task UpdateLastPurchaseDateAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE im
            SET im.LastPurchaseDate = pv.JobDate,
                im.UpdatedDate = GETDATE()
            FROM InventoryMaster im
            INNER JOIN (
                SELECT DISTINCT 
                    ProductCode, GradeCode, ClassCode, 
                    ShippingMarkCode, ShippingMarkName,
                    MAX(JobDate) as JobDate
                FROM PurchaseVouchers
                WHERE JobDate = @JobDate
                GROUP BY ProductCode, GradeCode, ClassCode, 
                         ShippingMarkCode, ShippingMarkName
            ) pv ON 
                im.ProductCode = pv.ProductCode AND
                im.GradeCode = pv.GradeCode AND
                im.ClassCode = pv.ClassCode AND
                im.ShippingMarkCode = pv.ShippingMarkCode AND
                im.ShippingMarkName = pv.ShippingMarkName
            WHERE pv.JobDate > ISNULL(im.LastPurchaseDate, '1900-01-01')";
        
        try
        {
            using var connection = CreateConnection();
            var affected = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            LogInfo($"最終仕入日を更新しました: {affected}件", new { jobDate, affected });
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateLastPurchaseDateAsync), new { jobDate });
            throw;
        }
    }
}