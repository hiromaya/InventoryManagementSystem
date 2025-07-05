using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Data.Repositories;

public class CpInventoryRepository : BaseRepository, ICpInventoryRepository
{
    public CpInventoryRepository(string connectionString, ILogger<CpInventoryRepository> logger) : base(connectionString, logger)
    {
    }

    public async Task<int> CreateCpInventoryFromInventoryMasterAsync(string dataSetId, DateTime jobDate)
    {
        const string sql = """
            INSERT INTO CpInventoryMaster (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice,
                DailyStock, DailyStockAmount, DailyUnitPrice,
                DailyFlag, DataSetId
            )
            SELECT 
                ProductCode, 
                GradeCode, 
                ClassCode, 
                ShippingMarkCode, 
                -- 文字化け対策：COLLATE指定
                ShippingMarkName COLLATE Japanese_CI_AS,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                @JobDate, GETDATE(), GETDATE(),
                CurrentStock, CurrentStockAmount, CASE WHEN CurrentStock > 0 THEN CurrentStockAmount / CurrentStock ELSE 0 END,
                CurrentStock, CurrentStockAmount, CASE WHEN CurrentStock > 0 THEN CurrentStockAmount / CurrentStock ELSE 0 END,
                '9', @DataSetId
            FROM InventoryMaster
            WHERE JobDate = @JobDate
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, JobDate = jobDate });
    }

    public async Task<int> ClearDailyAreaAsync(string dataSetId)
    {
        const string sql = """
            UPDATE CpInventoryMaster 
            SET 
                DailySalesQuantity = 0, DailySalesAmount = 0,
                DailySalesReturnQuantity = 0, DailySalesReturnAmount = 0,
                DailyPurchaseQuantity = 0, DailyPurchaseAmount = 0,
                DailyPurchaseReturnQuantity = 0, DailyPurchaseReturnAmount = 0,
                DailyInventoryAdjustmentQuantity = 0, DailyInventoryAdjustmentAmount = 0,
                DailyProcessingQuantity = 0, DailyProcessingAmount = 0,
                DailyTransferQuantity = 0, DailyTransferAmount = 0,
                DailyReceiptQuantity = 0, DailyReceiptAmount = 0,
                DailyShipmentQuantity = 0, DailyShipmentAmount = 0,
                DailyGrossProfit = 0, DailyWalkingAmount = 0,
                DailyIncentiveAmount = 0, DailyDiscountAmount = 0,
                DailyStock = 0, DailyStockAmount = 0, DailyUnitPrice = 0,
                DailyFlag = '9',
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });
    }

    public async Task<CpInventoryMaster?> GetByKeyAsync(InventoryKey key, string dataSetId)
    {
        const string sql = """
            SELECT * FROM CpInventoryMaster 
            WHERE ProductCode = @ProductCode 
                AND GradeCode = @GradeCode 
                AND ClassCode = @ClassCode 
                AND ShippingMarkCode = @ShippingMarkCode 
                AND ShippingMarkName COLLATE Japanese_CI_AS = @ShippingMarkName COLLATE Japanese_CI_AS
                AND DataSetId = @DataSetId
            """;

        using var connection = new SqlConnection(_connectionString);
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new 
        { 
            key.ProductCode, 
            key.GradeCode, 
            key.ClassCode, 
            key.ShippingMarkCode, 
            key.ShippingMarkName,
            DataSetId = dataSetId 
        });

        if (result == null) return null;

        return MapToCpInventoryMaster(result);
    }

    public async Task<IEnumerable<CpInventoryMaster>> GetAllAsync(string dataSetId)
    {
        const string sql = "SELECT * FROM CpInventoryMaster WHERE DataSetId = @DataSetId";

        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(sql, new { DataSetId = dataSetId });

        return results.Select(MapToCpInventoryMaster);
    }

    public async Task<int> UpdateAsync(CpInventoryMaster cpInventory)
    {
        const string sql = """
            UPDATE CpInventoryMaster 
            SET 
                ProductName = @ProductName, Unit = @Unit, StandardPrice = @StandardPrice,
                ProductCategory1 = @ProductCategory1, ProductCategory2 = @ProductCategory2,
                UpdatedDate = @UpdatedDate,
                PreviousDayStock = @PreviousDayStock, PreviousDayStockAmount = @PreviousDayStockAmount,
                PreviousDayUnitPrice = @PreviousDayUnitPrice,
                DailyStock = @DailyStock, DailyStockAmount = @DailyStockAmount, DailyUnitPrice = @DailyUnitPrice,
                DailyFlag = @DailyFlag,
                DailySalesQuantity = @DailySalesQuantity, DailySalesAmount = @DailySalesAmount,
                DailySalesReturnQuantity = @DailySalesReturnQuantity, DailySalesReturnAmount = @DailySalesReturnAmount,
                DailyPurchaseQuantity = @DailyPurchaseQuantity, DailyPurchaseAmount = @DailyPurchaseAmount,
                DailyPurchaseReturnQuantity = @DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount = @DailyPurchaseReturnAmount,
                DailyInventoryAdjustmentQuantity = @DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount = @DailyInventoryAdjustmentAmount,
                DailyProcessingQuantity = @DailyProcessingQuantity, DailyProcessingAmount = @DailyProcessingAmount,
                DailyTransferQuantity = @DailyTransferQuantity, DailyTransferAmount = @DailyTransferAmount,
                DailyReceiptQuantity = @DailyReceiptQuantity, DailyReceiptAmount = @DailyReceiptAmount,
                DailyShipmentQuantity = @DailyShipmentQuantity, DailyShipmentAmount = @DailyShipmentAmount,
                DailyGrossProfit = @DailyGrossProfit, DailyWalkingAmount = @DailyWalkingAmount,
                DailyIncentiveAmount = @DailyIncentiveAmount, DailyDiscountAmount = @DailyDiscountAmount
            WHERE ProductCode = @ProductCode 
                AND GradeCode = @GradeCode 
                AND ClassCode = @ClassCode 
                AND ShippingMarkCode = @ShippingMarkCode 
                AND ShippingMarkName COLLATE Japanese_CI_AS = @ShippingMarkName COLLATE Japanese_CI_AS
                AND DataSetId = @DataSetId
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new
        {
            cpInventory.ProductName, cpInventory.Unit, cpInventory.StandardPrice,
            cpInventory.ProductCategory1, cpInventory.ProductCategory2,
            cpInventory.UpdatedDate,
            cpInventory.PreviousDayStock, cpInventory.PreviousDayStockAmount, cpInventory.PreviousDayUnitPrice,
            cpInventory.DailyStock, cpInventory.DailyStockAmount, cpInventory.DailyUnitPrice,
            cpInventory.DailyFlag,
            cpInventory.DailySalesQuantity, cpInventory.DailySalesAmount,
            cpInventory.DailySalesReturnQuantity, cpInventory.DailySalesReturnAmount,
            cpInventory.DailyPurchaseQuantity, cpInventory.DailyPurchaseAmount,
            cpInventory.DailyPurchaseReturnQuantity, cpInventory.DailyPurchaseReturnAmount,
            cpInventory.DailyInventoryAdjustmentQuantity, cpInventory.DailyInventoryAdjustmentAmount,
            cpInventory.DailyProcessingQuantity, cpInventory.DailyProcessingAmount,
            cpInventory.DailyTransferQuantity, cpInventory.DailyTransferAmount,
            cpInventory.DailyReceiptQuantity, cpInventory.DailyReceiptAmount,
            cpInventory.DailyShipmentQuantity, cpInventory.DailyShipmentAmount,
            cpInventory.DailyGrossProfit, cpInventory.DailyWalkingAmount,
            cpInventory.DailyIncentiveAmount, cpInventory.DailyDiscountAmount,
            cpInventory.Key.ProductCode, cpInventory.Key.GradeCode, cpInventory.Key.ClassCode,
            cpInventory.Key.ShippingMarkCode, cpInventory.Key.ShippingMarkName,
            cpInventory.DataSetId
        });
    }

    public async Task<int> UpdateBatchAsync(IEnumerable<CpInventoryMaster> cpInventories)
    {
        if (!cpInventories.Any()) return 0;

        const string sql = """
            UPDATE CpInventoryMaster 
            SET 
                DailyStock = @DailyStock, DailyStockAmount = @DailyStockAmount, DailyUnitPrice = @DailyUnitPrice,
                DailyFlag = @DailyFlag, UpdatedDate = @UpdatedDate
            WHERE ProductCode = @ProductCode 
                AND GradeCode = @GradeCode 
                AND ClassCode = @ClassCode 
                AND ShippingMarkCode = @ShippingMarkCode 
                AND ShippingMarkName COLLATE Japanese_CI_AS = @ShippingMarkName COLLATE Japanese_CI_AS
                AND DataSetId = @DataSetId
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, cpInventories.Select(cp => new
        {
            cp.DailyStock, cp.DailyStockAmount, cp.DailyUnitPrice,
            cp.DailyFlag, cp.UpdatedDate,
            cp.Key.ProductCode, cp.Key.GradeCode, cp.Key.ClassCode,
            cp.Key.ShippingMarkCode, cp.Key.ShippingMarkName,
            cp.DataSetId
        }));
    }

    public async Task<int> AggregateSalesDataAsync(string dataSetId, DateTime jobDate)
    {
        const string sql = """
            UPDATE cp
            SET 
                DailySalesQuantity = ISNULL(sales.SalesQuantity, 0),
                DailySalesAmount = ISNULL(sales.SalesAmount, 0),
                -- 売上データが存在する場合のみDailyFlagを'0'に更新
                DailyFlag = CASE 
                    WHEN sales.ProductCode IS NOT NULL THEN '0' 
                    ELSE cp.DailyFlag 
                END,
                UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(ABS(Quantity)) as SalesQuantity,
                    SUM(ABS(Amount)) as SalesAmount
                FROM SalesVouchers 
                WHERE JobDate = @JobDate 
                    AND VoucherType IN ('51', '52')
                    AND DetailType IN ('1', '2')
                    AND Quantity <> 0
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) sales ON cp.ProductCode = sales.ProductCode 
                AND cp.GradeCode = sales.GradeCode 
                AND cp.ClassCode = sales.ClassCode 
                AND cp.ShippingMarkCode = sales.ShippingMarkCode
                AND cp.ShippingMarkName COLLATE Japanese_CI_AS = sales.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, JobDate = jobDate });
    }

    public async Task<int> AggregatePurchaseDataAsync(string dataSetId, DateTime jobDate)
    {
        const string sql = """
            UPDATE cp
            SET 
                DailyPurchaseQuantity = ISNULL(purchase.PurchaseQuantity, 0),
                DailyPurchaseAmount = ISNULL(purchase.PurchaseAmount, 0),
                -- 仕入データが存在する場合のみDailyFlagを'0'に更新
                DailyFlag = CASE 
                    WHEN purchase.ProductCode IS NOT NULL THEN '0' 
                    ELSE cp.DailyFlag 
                END,
                UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(Quantity) as PurchaseQuantity,
                    SUM(Amount) as PurchaseAmount
                FROM PurchaseVouchers 
                WHERE JobDate = @JobDate 
                    AND VoucherType IN ('11', '12')
                    AND DetailType IN ('1', '2')
                    AND Quantity <> 0
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) purchase ON cp.ProductCode = purchase.ProductCode 
                AND cp.GradeCode = purchase.GradeCode 
                AND cp.ClassCode = purchase.ClassCode 
                AND cp.ShippingMarkCode = purchase.ShippingMarkCode
                AND cp.ShippingMarkName COLLATE Japanese_CI_AS = purchase.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, JobDate = jobDate });
    }

    public async Task<int> AggregateInventoryAdjustmentDataAsync(string dataSetId, DateTime jobDate)
    {
        const string sql = """
            UPDATE cp
            SET 
                DailyInventoryAdjustmentQuantity = ISNULL(adj.AdjustmentQuantity, 0),
                DailyInventoryAdjustmentAmount = ISNULL(adj.AdjustmentAmount, 0),
                -- 在庫調整データが存在する場合のみDailyFlagを'0'に更新
                DailyFlag = CASE 
                    WHEN adj.ProductCode IS NOT NULL THEN '0' 
                    ELSE cp.DailyFlag 
                END,
                UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(Quantity) as AdjustmentQuantity,
                    SUM(Amount) as AdjustmentAmount
                FROM InventoryAdjustments 
                WHERE JobDate = @JobDate 
                    AND VoucherType IN ('71', '72')
                    AND DetailType IN ('1', '3', '4')
                    AND Quantity <> 0
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) adj ON cp.ProductCode = adj.ProductCode 
                AND cp.GradeCode = adj.GradeCode 
                AND cp.ClassCode = adj.ClassCode 
                AND cp.ShippingMarkCode = adj.ShippingMarkCode
                AND cp.ShippingMarkName COLLATE Japanese_CI_AS = adj.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, JobDate = jobDate });
    }

    public async Task<int> CalculateDailyStockAsync(string dataSetId)
    {
        const string sql = """
            UPDATE CpInventoryMaster 
            SET 
                DailyStock = PreviousDayStock + DailyPurchaseQuantity + DailyInventoryAdjustmentQuantity - DailySalesQuantity,
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });
    }

    public async Task<int> SetDailyFlagToProcessedAsync(string dataSetId)
    {
        // このメソッドは使用しないため無効化
        return 0;
    }

    public async Task<int> DeleteByDataSetIdAsync(string dataSetId)
    {
        const string sql = "DELETE FROM CpInventoryMaster WHERE DataSetId = @DataSetId";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });
    }

    public async Task<int> RepairShippingMarkNamesAsync(string dataSetId)
    {
        const string sql = """
            UPDATE cp
            SET cp.ShippingMarkName = im.ShippingMarkName
            FROM CpInventoryMaster cp
            INNER JOIN InventoryMaster im ON 
                cp.ProductCode = im.ProductCode AND
                cp.GradeCode = im.GradeCode AND
                cp.ClassCode = im.ClassCode AND
                cp.ShippingMarkCode = im.ShippingMarkCode
            WHERE cp.DataSetId = @DataSetId
                AND cp.ShippingMarkName LIKE '%?%'
            """;
        
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });
    }

    public async Task<int> CountGarbledShippingMarkNamesAsync(string dataSetId)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM CpInventoryMaster
            WHERE DataSetId = @DataSetId
                AND ShippingMarkName LIKE '%?%'
            """;
        
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, new { DataSetId = dataSetId });
    }

    public async Task<int> DeleteAllAsync()
    {
        const string sql = "DELETE FROM CpInventoryMaster";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var deletedCount = await connection.ExecuteAsync(sql);
            
            _logger.LogInformation("CP在庫マスタ全削除完了: {Count}件", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CP在庫マスタ全削除エラー");
            throw;
        }
    }

    public async Task<InventorySystem.Core.Models.AggregationResult> GetAggregationResultAsync(string dataSetId)
    {
        const string sql = """
            SELECT 
                COUNT(*) as TotalCount,
                SUM(CASE WHEN DailyFlag = '0' THEN 1 ELSE 0 END) as AggregatedCount,
                SUM(CASE WHEN DailyFlag = '9' THEN 1 ELSE 0 END) as NotAggregatedCount,
                SUM(CASE WHEN DailyFlag = '0' AND DailyPurchaseQuantity = 0 AND DailySalesQuantity = 0 THEN 1 ELSE 0 END) as ZeroTransactionCount
            FROM CpInventoryMaster
            WHERE DataSetId = @DataSetId
            """;
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QuerySingleAsync<dynamic>(sql, new { DataSetId = dataSetId });
            
            return new InventorySystem.Core.Models.AggregationResult
            {
                TotalCount = result.TotalCount ?? 0,
                AggregatedCount = result.AggregatedCount ?? 0,
                NotAggregatedCount = result.NotAggregatedCount ?? 0,
                ZeroTransactionCount = result.ZeroTransactionCount ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "集計結果取得エラー: DataSetId={DataSetId}", dataSetId);
            throw;
        }
    }

    private static CpInventoryMaster MapToCpInventoryMaster(dynamic row)
    {
        return new CpInventoryMaster
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
            PreviousDayStock = row.PreviousDayStock ?? 0m,
            PreviousDayStockAmount = row.PreviousDayStockAmount ?? 0m,
            PreviousDayUnitPrice = row.PreviousDayUnitPrice ?? 0m,
            DailyStock = row.DailyStock ?? 0m,
            DailyStockAmount = row.DailyStockAmount ?? 0m,
            DailyUnitPrice = row.DailyUnitPrice ?? 0m,
            DailyFlag = ConvertToChar(row.DailyFlag),
            DailySalesQuantity = row.DailySalesQuantity ?? 0m,
            DailySalesAmount = row.DailySalesAmount ?? 0m,
            DailySalesReturnQuantity = row.DailySalesReturnQuantity ?? 0m,
            DailySalesReturnAmount = row.DailySalesReturnAmount ?? 0m,
            DailyPurchaseQuantity = row.DailyPurchaseQuantity ?? 0m,
            DailyPurchaseAmount = row.DailyPurchaseAmount ?? 0m,
            DailyPurchaseReturnQuantity = row.DailyPurchaseReturnQuantity ?? 0m,
            DailyPurchaseReturnAmount = row.DailyPurchaseReturnAmount ?? 0m,
            DailyInventoryAdjustmentQuantity = row.DailyInventoryAdjustmentQuantity ?? 0m,
            DailyInventoryAdjustmentAmount = row.DailyInventoryAdjustmentAmount ?? 0m,
            DailyProcessingQuantity = row.DailyProcessingQuantity ?? 0m,
            DailyProcessingAmount = row.DailyProcessingAmount ?? 0m,
            DailyTransferQuantity = row.DailyTransferQuantity ?? 0m,
            DailyTransferAmount = row.DailyTransferAmount ?? 0m,
            DailyReceiptQuantity = row.DailyReceiptQuantity ?? 0m,
            DailyReceiptAmount = row.DailyReceiptAmount ?? 0m,
            DailyShipmentQuantity = row.DailyShipmentQuantity ?? 0m,
            DailyShipmentAmount = row.DailyShipmentAmount ?? 0m,
            DailyGrossProfit = row.DailyGrossProfit ?? 0m,
            DailyWalkingAmount = row.DailyWalkingAmount ?? 0m,
            DailyIncentiveAmount = row.DailyIncentiveAmount ?? 0m,
            DailyDiscountAmount = row.DailyDiscountAmount ?? 0m,
            // 月計項目
            MonthlySalesQuantity = row.MonthlySalesQuantity ?? 0m,
            MonthlySalesAmount = row.MonthlySalesAmount ?? 0m,
            MonthlySalesReturnQuantity = row.MonthlySalesReturnQuantity ?? 0m,
            MonthlySalesReturnAmount = row.MonthlySalesReturnAmount ?? 0m,
            MonthlyPurchaseQuantity = row.MonthlyPurchaseQuantity ?? 0m,
            MonthlyPurchaseAmount = row.MonthlyPurchaseAmount ?? 0m,
            MonthlyGrossProfit = row.MonthlyGrossProfit ?? 0m,
            MonthlyWalkingAmount = row.MonthlyWalkingAmount ?? 0m,
            MonthlyIncentiveAmount = row.MonthlyIncentiveAmount ?? 0m,
            DataSetId = row.DataSetId ?? string.Empty
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
    
    public async Task<int> GetCountAsync(DateTime jobDate)
    {
        const string sql = "SELECT COUNT(*) FROM CpInventoryMaster WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate });
    }
    
    /// <summary>
    /// 売上月計を更新
    /// </summary>
    public async Task<int> UpdateMonthlySalesAsync(DateTime monthStartDate, DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET 
                cp.MonthlySalesQuantity = ISNULL(s.Quantity, 0),
                cp.MonthlySalesAmount = ISNULL(s.Amount, 0),
                cp.MonthlySalesReturnQuantity = ISNULL(s.ReturnQuantity, 0),
                cp.MonthlySalesReturnAmount = ISNULL(s.ReturnAmount, 0)
            FROM CP_InventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(CASE WHEN DetailType = 1 THEN Quantity ELSE 0 END) as Quantity,
                    SUM(CASE WHEN DetailType = 1 THEN Amount ELSE 0 END) as Amount,
                    SUM(CASE WHEN DetailType = 2 THEN Quantity ELSE 0 END) as ReturnQuantity,
                    SUM(CASE WHEN DetailType = 2 THEN Amount ELSE 0 END) as ReturnAmount
                FROM SalesVouchers
                WHERE JobDate >= @monthStartDate AND JobDate <= @jobDate
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) s ON cp.ProductCode = s.ProductCode 
                AND cp.GradeCode = s.GradeCode 
                AND cp.ClassCode = s.ClassCode 
                AND cp.ShippingMarkCode = s.ShippingMarkCode 
                AND cp.ShippingMarkName = s.ShippingMarkName
            WHERE cp.JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { monthStartDate, jobDate });
    }

    /// <summary>
    /// 仕入月計を更新
    /// </summary>
    public async Task<int> UpdateMonthlyPurchaseAsync(DateTime monthStartDate, DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET 
                cp.MonthlyPurchaseQuantity = ISNULL(p.Quantity, 0),
                cp.MonthlyPurchaseAmount = ISNULL(p.Amount, 0)
            FROM CP_InventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(Quantity) as Quantity,
                    SUM(Amount) as Amount
                FROM PurchaseVouchers
                WHERE JobDate >= @monthStartDate AND JobDate <= @jobDate
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) p ON cp.ProductCode = p.ProductCode 
                AND cp.GradeCode = p.GradeCode 
                AND cp.ClassCode = p.ClassCode 
                AND cp.ShippingMarkCode = p.ShippingMarkCode 
                AND cp.ShippingMarkName = p.ShippingMarkName
            WHERE cp.JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { monthStartDate, jobDate });
    }

    /// <summary>
    /// 月計粗利益を計算
    /// </summary>
    public async Task<int> CalculateMonthlyGrossProfitAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE CP_InventoryMaster
            SET 
                MonthlyGrossProfit = (MonthlySalesAmount + MonthlySalesReturnAmount) 
                                   - (MonthlyPurchaseAmount * (DailyUnitPrice / NULLIF(StandardPrice, 0)))
            WHERE JobDate = @jobDate 
              AND StandardPrice > 0";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { jobDate });
    }
}