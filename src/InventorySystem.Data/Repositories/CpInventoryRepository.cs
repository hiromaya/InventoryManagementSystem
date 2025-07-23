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

    public async Task<int> CreateCpInventoryFromInventoryMasterAsync(string dataSetId, DateTime? jobDate)
    {
        // 累積管理対応版：在庫マスタのレコードをCP在庫マスタにコピー
        // jobDateがnullの場合は全期間対象
        using var connection = new SqlConnection(_connectionString);
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
            "sp_CreateCpInventoryFromInventoryMasterCumulative",
            new { DataSetId = dataSetId, JobDate = jobDate },
            commandType: CommandType.StoredProcedure);
        
        return result?.CreatedCount ?? 0;
    }

    public async Task<int> ClearDailyAreaAsync(string dataSetId)
    {
        const string sql = """
            UPDATE CpInventoryMaster 
            SET 
                -- 当日エリアのクリア
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
                -- 月計フィールドも明示的に0で初期化（アンマッチリストでのエラー回避）
                MonthlySalesQuantity = 0, MonthlySalesAmount = 0,
                MonthlySalesReturnQuantity = 0, MonthlySalesReturnAmount = 0,
                MonthlyPurchaseQuantity = 0, MonthlyPurchaseAmount = 0,
                MonthlyPurchaseReturnQuantity = 0, MonthlyPurchaseReturnAmount = 0,
                MonthlyInventoryAdjustmentQuantity = 0, MonthlyInventoryAdjustmentAmount = 0,
                MonthlyProcessingQuantity = 0, MonthlyProcessingAmount = 0,
                MonthlyTransferQuantity = 0, MonthlyTransferAmount = 0,
                MonthlyGrossProfit = 0, MonthlyWalkingAmount = 0,
                MonthlyIncentiveAmount = 0,
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

    public async Task<int> AggregateSalesDataAsync(string dataSetId, DateTime? jobDate)
    {
        // jobDateがnullの場合は全期間対象
        var dateCondition = jobDate.HasValue ? "WHERE JobDate = @JobDate" : "";
        
        var sql = $"""
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
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('51', '52')
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

    public async Task<int> AggregatePurchaseDataAsync(string dataSetId, DateTime? jobDate)
    {
        // jobDateがnullの場合は全期間対象
        var dateCondition = jobDate.HasValue ? "WHERE JobDate = @JobDate" : "";
        
        var sql = $"""
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
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('11', '12')
                    AND DetailType IN ('1', '2')  -- 仕入、返品のみ（値引は別途計算）
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

    public async Task<int> AggregateInventoryAdjustmentDataAsync(string dataSetId, DateTime? jobDate)
    {
        using var connection = new SqlConnection(_connectionString);
        int totalUpdated = 0;

        // jobDateがnullの場合は全期間対象
        var dateCondition = jobDate.HasValue ? "WHERE JobDate = @JobDate" : "";

        // 1. 在庫調整（単位コード: 01, 03, 06）
        var adjustmentSql = $"""
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
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('71', '72')
                    AND DetailType IN ('1', '3', '4')
                    AND CategoryCode IN (1, 3, 6)  -- 在庫調整の単位コード
                    AND Quantity <> 0
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) adj ON cp.ProductCode = adj.ProductCode 
                AND cp.GradeCode = adj.GradeCode 
                AND cp.ClassCode = adj.ClassCode 
                AND cp.ShippingMarkCode = adj.ShippingMarkCode
                AND cp.ShippingMarkName COLLATE Japanese_CI_AS = adj.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId
            """;
        
        var adjustmentResult = await connection.ExecuteAsync(adjustmentSql, new { DataSetId = dataSetId, JobDate = jobDate });
        totalUpdated += adjustmentResult;
        
        // 2. 加工費（単位コード: 02, 05）
        var processingSql = $"""
            UPDATE cp
            SET 
                DailyProcessingQuantity = ISNULL(adj.ProcessingQuantity, 0),
                DailyProcessingAmount = ISNULL(adj.ProcessingAmount, 0),
                -- 加工費データが存在する場合のみDailyFlagを'0'に更新
                DailyFlag = CASE 
                    WHEN adj.ProductCode IS NOT NULL THEN '0' 
                    ELSE cp.DailyFlag 
                END,
                UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(Quantity) as ProcessingQuantity,
                    SUM(Amount) as ProcessingAmount
                FROM InventoryAdjustments 
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('71', '72')
                    AND DetailType IN ('1', '3', '4')
                    AND CategoryCode IN (2, 5)  -- 加工費の単位コード
                    AND Quantity <> 0
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) adj ON cp.ProductCode = adj.ProductCode 
                AND cp.GradeCode = adj.GradeCode 
                AND cp.ClassCode = adj.ClassCode 
                AND cp.ShippingMarkCode = adj.ShippingMarkCode
                AND cp.ShippingMarkName COLLATE Japanese_CI_AS = adj.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId
            """;
        
        var processingResult = await connection.ExecuteAsync(processingSql, new { DataSetId = dataSetId, JobDate = jobDate });
        totalUpdated += processingResult;
        
        // 3. 振替（単位コード: 04）
        var transferSql = $"""
            UPDATE cp
            SET 
                DailyTransferQuantity = ISNULL(adj.TransferQuantity, 0),
                DailyTransferAmount = ISNULL(adj.TransferAmount, 0),
                -- 振替データが存在する場合のみDailyFlagを'0'に更新
                DailyFlag = CASE 
                    WHEN adj.ProductCode IS NOT NULL THEN '0' 
                    ELSE cp.DailyFlag 
                END,
                UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(Quantity) as TransferQuantity,
                    SUM(Amount) as TransferAmount
                FROM InventoryAdjustments 
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('71', '72')
                    AND DetailType IN ('1', '3', '4')
                    AND CategoryCode = 4  -- 振替の単位コード
                    AND Quantity <> 0
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) adj ON cp.ProductCode = adj.ProductCode 
                AND cp.GradeCode = adj.GradeCode 
                AND cp.ClassCode = adj.ClassCode 
                AND cp.ShippingMarkCode = adj.ShippingMarkCode
                AND cp.ShippingMarkName COLLATE Japanese_CI_AS = adj.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId
            """;
        
        var transferResult = await connection.ExecuteAsync(transferSql, new { DataSetId = dataSetId, JobDate = jobDate });
        totalUpdated += transferResult;
        
        return totalUpdated;
    }

    public async Task<int> CalculateDailyStockAsync(string dataSetId)
    {
        const string sql = @"
            UPDATE CpInventoryMaster
            SET 
                -- 当日在庫数量の計算（移動平均法による正確な計算）
                -- 前日在庫 + 入荷（仕入-仕返） - 出荷（売上-売返） - 在庫調整 - 加工 - 振替
                DailyStock = PreviousDayStock + 
                             (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) - 
                             (DailySalesQuantity - DailySalesReturnQuantity) -
                             DailyInventoryAdjustmentQuantity - 
                             DailyProcessingQuantity -
                             DailyTransferQuantity,
                
                -- 当日在庫単価の計算（移動平均法）
                -- 入荷がある場合の移動平均単価計算
                DailyUnitPrice = CASE 
                    WHEN (PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity) = 0 THEN 0
                    ELSE ROUND((PreviousDayStockAmount + DailyPurchaseAmount - DailyPurchaseReturnAmount) / 
                               NULLIF(PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity, 0), 4)
                END,
                
                -- 当日在庫金額の計算
                -- 当日在庫数量 × 当日在庫単価
                DailyStockAmount = ROUND(
                    (PreviousDayStock + 
                     (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) - 
                     (DailySalesQuantity - DailySalesReturnQuantity) -
                     DailyInventoryAdjustmentQuantity - 
                     DailyProcessingQuantity -
                     DailyTransferQuantity) * 
                    CASE 
                        WHEN (PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity) = 0 THEN 0
                        ELSE ROUND((PreviousDayStockAmount + DailyPurchaseAmount - DailyPurchaseReturnAmount) / 
                                   NULLIF(PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity, 0), 4)
                    END, 4),
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId";

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
                cp.MonthlySalesReturnAmount = ISNULL(s.ReturnAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(CASE WHEN DetailType = '1' THEN Quantity ELSE 0 END) as Quantity,
                    SUM(CASE WHEN DetailType = '1' THEN Amount ELSE 0 END) as Amount,
                    SUM(CASE WHEN DetailType = '2' THEN Quantity ELSE 0 END) as ReturnQuantity,
                    SUM(CASE WHEN DetailType = '2' THEN Amount ELSE 0 END) as ReturnAmount
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
                cp.MonthlyPurchaseAmount = ISNULL(p.Amount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
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
            UPDATE CpInventoryMaster
            SET 
                MonthlyGrossProfit = (MonthlySalesAmount - MonthlySalesReturnAmount) 
                                   - (MonthlyPurchaseAmount * 
                                      CASE 
                                        WHEN StandardPrice > 0 THEN (DailyUnitPrice / StandardPrice)
                                        ELSE 0
                                      END),
                UpdatedDate = GETDATE()
            WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { jobDate });
    }

    /// <summary>
    /// 在庫調整月計を更新
    /// </summary>
    public async Task<int> UpdateMonthlyInventoryAdjustmentAsync(DateTime monthStartDate, DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET 
                cp.MonthlyInventoryAdjustmentQuantity = ISNULL(a.Quantity, 0),
                cp.MonthlyInventoryAdjustmentAmount = ISNULL(a.Amount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(Quantity) as Quantity,
                    SUM(Amount) as Amount
                FROM InventoryAdjustments
                WHERE JobDate >= @monthStartDate AND JobDate <= @jobDate
                    AND VoucherType IN ('71', '72')
                    AND DetailType IN ('1', '3', '4')
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) a ON cp.ProductCode = a.ProductCode 
                AND cp.GradeCode = a.GradeCode 
                AND cp.ClassCode = a.ClassCode 
                AND cp.ShippingMarkCode = a.ShippingMarkCode 
                AND cp.ShippingMarkName = a.ShippingMarkName
            WHERE cp.JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { monthStartDate, jobDate });
    }
    
    /// <summary>
    /// 仕入値引を集計する
    /// </summary>
    public async Task<int> CalculatePurchaseDiscountAsync(string dataSetId, DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET cp.DailyDiscountAmount = ISNULL(pv.DiscountAmount, 0)
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(Amount) as DiscountAmount
                FROM PurchaseVouchers
                WHERE JobDate = @jobDate
                    AND VoucherType IN ('11', '12')
                    AND DetailType = '3'  -- 単品値引
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) pv ON cp.ProductCode = pv.ProductCode 
                AND cp.GradeCode = pv.GradeCode 
                AND cp.ClassCode = pv.ClassCode 
                AND cp.ShippingMarkCode = pv.ShippingMarkCode 
                AND cp.ShippingMarkName = pv.ShippingMarkName
            WHERE cp.DataSetId = @dataSetId";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { dataSetId, jobDate });
    }

    /// <summary>
    /// 奨励金を計算する（仕入先分類1='01'の場合、仕入金額の1%）
    /// </summary>
    public async Task<int> CalculateIncentiveAsync(string dataSetId, DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET cp.DailyIncentiveAmount = ISNULL(pv.IncentiveAmount, 0)
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ShippingMarkName,
                    SUM(CASE WHEN sm.SupplierCategory1 = '01' THEN pv.Amount * 0.01 ELSE 0 END) as IncentiveAmount
                FROM PurchaseVouchers pv
                LEFT JOIN SupplierMaster sm ON pv.SupplierCode = sm.SupplierCode
                WHERE pv.JobDate = @jobDate
                    AND pv.VoucherType IN ('11', '12')
                    AND pv.DetailType IN ('1', '3')  -- 仕入、単品値引
                GROUP BY pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ShippingMarkName
            ) pv ON cp.ProductCode = pv.ProductCode 
                AND cp.GradeCode = pv.GradeCode 
                AND cp.ClassCode = pv.ClassCode 
                AND cp.ShippingMarkCode = pv.ShippingMarkCode 
                AND cp.ShippingMarkName = pv.ShippingMarkName
            WHERE cp.DataSetId = @dataSetId";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { dataSetId, jobDate });
    }

    /// <summary>
    /// 歩引き金を計算する（得意先マスタの歩引き率×売上金額）
    /// </summary>
    public async Task<int> CalculateWalkingAmountAsync(string dataSetId, DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET cp.DailyWalkingAmount = ISNULL(sv.WalkingAmount, 0)
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ShippingMarkName,
                    SUM(sv.Amount * ISNULL(cm.WalkingRate, 0) / 100) as WalkingAmount
                FROM SalesVouchers sv
                LEFT JOIN CustomerMaster cm ON sv.CustomerCode = cm.CustomerCode
                WHERE sv.JobDate = @jobDate
                    AND sv.VoucherType IN ('51', '52')
                    AND sv.DetailType IN ('1', '2', '3')  -- 売上、返品、単品値引
                GROUP BY sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ShippingMarkName
            ) sv ON cp.ProductCode = sv.ProductCode 
                AND cp.GradeCode = sv.GradeCode 
                AND cp.ClassCode = sv.ClassCode 
                AND cp.ShippingMarkCode = sv.ShippingMarkCode 
                AND cp.ShippingMarkName = sv.ShippingMarkName
            WHERE cp.DataSetId = @dataSetId";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { dataSetId, jobDate });
    }

    /// <summary>
    /// 在庫単価を計算する（移動平均法）
    /// </summary>
    public async Task<int> CalculateInventoryUnitPriceAsync(string dataSetId)
    {
        const string sql = @"
            UPDATE CpInventoryMaster
            SET
                -- ①仮在庫数 = 前日在庫数 + 当日入荷数（仕入-仕返）
                DailyReceiptQuantity = DailyPurchaseQuantity - DailyPurchaseReturnQuantity,
                -- ②仮在庫金額 = 前日在庫金額 + 当日入荷金額
                DailyReceiptAmount = DailyPurchaseAmount - DailyPurchaseReturnAmount,
                -- ③当日在庫単価 = 仮在庫金額 ÷ 仮在庫数（0除算対策、小数第5位四捨五入）
                DailyUnitPrice = CASE 
                    WHEN (PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity) = 0 THEN 0
                    ELSE ROUND((PreviousDayStockAmount + DailyPurchaseAmount - DailyPurchaseReturnAmount) / 
                               (PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity), 4)
                END,
                -- ④当日在庫数 = 前日在庫数 + 当日入荷数 - 当日出荷数
                DailyStock = PreviousDayStock + 
                             (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) - 
                             (DailySalesQuantity - DailySalesReturnQuantity) -
                             DailyInventoryAdjustmentQuantity - 
                             DailyProcessingQuantity -
                             DailyTransferQuantity,
                -- ⑤当日在庫金額 = 当日在庫数 × 当日在庫単価（小数第5位四捨五入）
                DailyStockAmount = ROUND(
                    (PreviousDayStock + 
                     (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) - 
                     (DailySalesQuantity - DailySalesReturnQuantity) -
                     DailyInventoryAdjustmentQuantity - 
                     DailyProcessingQuantity -
                     DailyTransferQuantity) * 
                    CASE 
                        WHEN (PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity) = 0 THEN 0
                        ELSE ROUND((PreviousDayStockAmount + DailyPurchaseAmount - DailyPurchaseReturnAmount) / 
                                   (PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity), 4)
                    END, 4),
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId";
        
        using var connection = CreateConnection();
        
        // 在庫単価計算
        var updateCount = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });
        
        // 粗利益の調整（在庫調整金額と加工費を減算）
        const string adjustGrossProfitSql = @"
            UPDATE CpInventoryMaster
            SET DailyGrossProfit = DailyGrossProfit - DailyInventoryAdjustmentAmount - DailyProcessingAmount,
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId";
        
        await connection.ExecuteAsync(adjustGrossProfitSql, new { DataSetId = dataSetId });
        
        return updateCount;
    }
    
    /// <summary>
    /// 粗利益を計算する（売上伝票1行ごと）
    /// </summary>
    public async Task<int> CalculateGrossProfitAsync(string dataSetId, DateTime jobDate)
    {
        using var connection = CreateConnection();
        
        // Step 1: 売上伝票の単価が0の場合、金額÷数量で単価を計算
        const string updateUnitPriceSql = @"
            UPDATE SalesVouchers
            SET UnitPrice = CASE 
                WHEN UnitPrice = 0 AND Quantity != 0 THEN ROUND(Amount / Quantity, 4)
                ELSE UnitPrice
            END
            WHERE JobDate = @JobDate AND UnitPrice = 0 AND Quantity != 0";
        
        await connection.ExecuteAsync(updateUnitPriceSql, new { JobDate = jobDate });
        
        // Step 2: 売上伝票1行ごとの粗利益計算
        const string calculateGrossProfitSql = @"
            UPDATE sv
            SET sv.GrossProfit = ROUND((sv.UnitPrice - ISNULL(cp.DailyUnitPrice, 0)) * sv.Quantity, 4)
            FROM SalesVouchers sv
            INNER JOIN CpInventoryMaster cp ON 
                sv.ProductCode = cp.ProductCode AND
                sv.GradeCode = cp.GradeCode AND
                sv.ClassCode = cp.ClassCode AND
                sv.ShippingMarkCode = cp.ShippingMarkCode AND
                sv.ShippingMarkName COLLATE Japanese_CI_AS = cp.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE sv.JobDate = @JobDate 
                AND cp.DataSetId = @DataSetId
                AND sv.VoucherType IN ('51', '52')
                AND sv.DetailType IN ('1', '2', '3')";
        
        await connection.ExecuteAsync(calculateGrossProfitSql, new { JobDate = jobDate, DataSetId = dataSetId });
        
        // Step 3: CP在庫Mの当日粗利益に集計
        const string aggregateGrossProfitSql = @"
            UPDATE cp
            SET cp.DailyGrossProfit = ISNULL(profit.TotalGrossProfit, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(ISNULL(GrossProfit, 0)) as TotalGrossProfit
                FROM SalesVouchers
                WHERE JobDate = @JobDate
                    AND VoucherType IN ('51', '52')
                    AND DetailType IN ('1', '2', '3')
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) profit ON 
                cp.ProductCode = profit.ProductCode AND
                cp.GradeCode = profit.GradeCode AND
                cp.ClassCode = profit.ClassCode AND
                cp.ShippingMarkCode = profit.ShippingMarkCode AND
                cp.ShippingMarkName COLLATE Japanese_CI_AS = profit.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId";
        
        var updateCount = await connection.ExecuteAsync(aggregateGrossProfitSql, new { JobDate = jobDate, DataSetId = dataSetId });
        
        // Step 4: 歩引き金額計算
        const string calculateWalkingAmountSql = @"
            UPDATE cp
            SET cp.DailyWalkingAmount = ISNULL(walk.WalkingAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ShippingMarkName,
                    SUM(ROUND(sv.Amount * ISNULL(c.WalkingRate, 0) / 100, 0)) as WalkingAmount
                FROM SalesVouchers sv
                INNER JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                WHERE sv.JobDate = @JobDate
                    AND sv.VoucherType IN ('51', '52')
                    AND sv.DetailType IN ('1', '2', '3')
                GROUP BY sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ShippingMarkName
            ) walk ON 
                cp.ProductCode = walk.ProductCode AND
                cp.GradeCode = walk.GradeCode AND
                cp.ClassCode = walk.ClassCode AND
                cp.ShippingMarkCode = walk.ShippingMarkCode AND
                cp.ShippingMarkName COLLATE Japanese_CI_AS = walk.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId";
        
        await connection.ExecuteAsync(calculateWalkingAmountSql, new { JobDate = jobDate, DataSetId = dataSetId });
        
        // Step 5: 奨励金計算
        const string calculateIncentiveAmountSql = @"
            UPDATE cp
            SET cp.DailyIncentiveAmount = ISNULL(inc.IncentiveAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ShippingMarkName,
                    SUM(ROUND(pv.Amount * 0.01, 0)) as IncentiveAmount
                FROM PurchaseVouchers pv
                INNER JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                WHERE pv.JobDate = @JobDate
                    AND pv.VoucherType IN ('11', '12')
                    AND pv.DetailType IN ('1', '3')
                    AND s.SupplierCategory1 = '01'
                GROUP BY pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ShippingMarkName
            ) inc ON 
                cp.ProductCode = inc.ProductCode AND
                cp.GradeCode = inc.GradeCode AND
                cp.ClassCode = inc.ClassCode AND
                cp.ShippingMarkCode = inc.ShippingMarkCode AND
                cp.ShippingMarkName COLLATE Japanese_CI_AS = inc.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId";
        
        await connection.ExecuteAsync(calculateIncentiveAmountSql, new { JobDate = jobDate, DataSetId = dataSetId });
        
        // Step 6: 仕入値引き計算
        const string calculateDiscountAmountSql = @"
            UPDATE cp
            SET cp.DailyDiscountAmount = ISNULL(disc.DiscountAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(ABS(Amount)) as DiscountAmount
                FROM PurchaseVouchers
                WHERE JobDate = @JobDate
                    AND VoucherType IN ('11', '12')
                    AND DetailType = '3'
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) disc ON 
                cp.ProductCode = disc.ProductCode AND
                cp.GradeCode = disc.GradeCode AND
                cp.ClassCode = disc.ClassCode AND
                cp.ShippingMarkCode = disc.ShippingMarkCode AND
                cp.ShippingMarkName COLLATE Japanese_CI_AS = disc.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId";
        
        await connection.ExecuteAsync(calculateDiscountAmountSql, new { JobDate = jobDate, DataSetId = dataSetId });
        
        return updateCount;
    }
    
    /// <summary>
    /// 前日の在庫マスタから前日在庫を引き継ぐ
    /// </summary>
    public async Task<int> InheritPreviousDayStockAsync(string dataSetId, DateTime jobDate, DateTime previousDate)
    {
        using var connection = CreateConnection();
        
        // 累積管理対応：在庫マスタの最新在庫情報を引き継ぐ（JobDateに依存しない）
        const string sql = @"
            -- 累積管理：在庫マスタから最新の在庫情報を引き継ぐ
            UPDATE cp
            SET 
                cp.PreviousDayStock = ISNULL(im.CurrentStock, 0),
                cp.PreviousDayStockAmount = ISNULL(im.CurrentStockAmount, 0),
                cp.PreviousDayUnitPrice = CASE 
                    WHEN ISNULL(im.CurrentStock, 0) > 0 
                    THEN ROUND(im.CurrentStockAmount / im.CurrentStock, 4)
                    ELSE 0 
                END,
                UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN InventoryMaster im
                ON cp.ProductCode = im.ProductCode
                AND cp.GradeCode = im.GradeCode
                AND cp.ClassCode = im.ClassCode
                AND cp.ShippingMarkCode = im.ShippingMarkCode
                AND cp.ShippingMarkName COLLATE Japanese_CI_AS = im.ShippingMarkName COLLATE Japanese_CI_AS
                -- 累積管理：JobDateの条件を削除（最新の在庫情報を使用）
            WHERE cp.DataSetId = @DataSetId;
            
            -- DailyStockも前日在庫で初期化（後の集計処理で正しい値に更新される）
            UPDATE CpInventoryMaster
            SET DailyStock = PreviousDayStock,
                DailyStockAmount = PreviousDayStockAmount,
                DailyUnitPrice = PreviousDayUnitPrice,
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId;";

        return await connection.ExecuteAsync(sql, new 
        { 
            DataSetId = dataSetId, 
            PreviousDate = previousDate  // パラメータは保持するが使用しない（後方互換性のため）
        });
    }

    /// <summary>
    /// 月計合計を計算する
    /// </summary>
    public async Task<int> CalculateMonthlyTotalsAsync(string dataSetId, DateTime jobDate)
    {
        using var connection = CreateConnection();
        
        // 月初日を計算
        var monthStartDate = new DateTime(jobDate.Year, jobDate.Month, 1);
        
        // 月計粗利益の計算
        const string calculateMonthlyGrossProfitSql = @"
            UPDATE cp
            SET cp.MonthlyGrossProfit = ISNULL(profit.TotalGrossProfit, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    SUM(ISNULL(GrossProfit, 0)) as TotalGrossProfit
                FROM SalesVouchers
                WHERE JobDate >= @MonthStartDate AND JobDate <= @JobDate
                    AND VoucherType IN ('51', '52')
                    AND DetailType IN ('1', '2', '3')
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            ) profit ON 
                cp.ProductCode = profit.ProductCode AND
                cp.GradeCode = profit.GradeCode AND
                cp.ClassCode = profit.ClassCode AND
                cp.ShippingMarkCode = profit.ShippingMarkCode AND
                cp.ShippingMarkName COLLATE Japanese_CI_AS = profit.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId";
        
        var updateCount = await connection.ExecuteAsync(calculateMonthlyGrossProfitSql, 
            new { MonthStartDate = monthStartDate, JobDate = jobDate, DataSetId = dataSetId });
        
        // 月計歩引き金額計算
        const string calculateMonthlyWalkingAmountSql = @"
            UPDATE cp
            SET cp.MonthlyWalkingAmount = ISNULL(walk.WalkingAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ShippingMarkName,
                    SUM(ROUND(sv.Amount * ISNULL(c.WalkingRate, 0) / 100, 0)) as WalkingAmount
                FROM SalesVouchers sv
                INNER JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                WHERE sv.JobDate >= @MonthStartDate AND sv.JobDate <= @JobDate
                    AND sv.VoucherType IN ('51', '52')
                    AND sv.DetailType IN ('1', '2', '3')
                GROUP BY sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ShippingMarkName
            ) walk ON 
                cp.ProductCode = walk.ProductCode AND
                cp.GradeCode = walk.GradeCode AND
                cp.ClassCode = walk.ClassCode AND
                cp.ShippingMarkCode = walk.ShippingMarkCode AND
                cp.ShippingMarkName COLLATE Japanese_CI_AS = walk.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId";
        
        await connection.ExecuteAsync(calculateMonthlyWalkingAmountSql, 
            new { MonthStartDate = monthStartDate, JobDate = jobDate, DataSetId = dataSetId });
        
        // 月計奨励金計算
        const string calculateMonthlyIncentiveAmountSql = @"
            UPDATE cp
            SET cp.MonthlyIncentiveAmount = ISNULL(inc.IncentiveAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ShippingMarkName,
                    SUM(ROUND(pv.Amount * 0.01, 0)) as IncentiveAmount
                FROM PurchaseVouchers pv
                INNER JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                WHERE pv.JobDate >= @MonthStartDate AND pv.JobDate <= @JobDate
                    AND pv.VoucherType IN ('11', '12')
                    AND pv.DetailType IN ('1', '3')
                    AND s.SupplierCategory1 = '01'
                GROUP BY pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ShippingMarkName
            ) inc ON 
                cp.ProductCode = inc.ProductCode AND
                cp.GradeCode = inc.GradeCode AND
                cp.ClassCode = inc.ClassCode AND
                cp.ShippingMarkCode = inc.ShippingMarkCode AND
                cp.ShippingMarkName COLLATE Japanese_CI_AS = inc.ShippingMarkName COLLATE Japanese_CI_AS
            WHERE cp.DataSetId = @DataSetId";
        
        await connection.ExecuteAsync(calculateMonthlyIncentiveAmountSql, 
            new { MonthStartDate = monthStartDate, JobDate = jobDate, DataSetId = dataSetId });
        
        return updateCount;
    }
    
    /// <summary>
    /// 古いCP在庫マスタデータをクリーンアップする
    /// </summary>
    /// <param name="cutoffDate">削除基準日（この日付より前のデータを削除）</param>
    /// <returns>削除件数</returns>
    public async Task<int> CleanupOldDataAsync(DateTime cutoffDate)
    {
        using var connection = CreateConnection();
        
        const string cleanupSql = @"
            DELETE FROM CpInventoryMaster 
            WHERE JobDate < @CutoffDate";
        
        var deletedCount = await connection.ExecuteAsync(cleanupSql, new { CutoffDate = cutoffDate });
        
        _logger.LogInformation("古いCP在庫マスタをクリーンアップしました - 削除件数: {Count} (基準日: {CutoffDate})", 
            deletedCount, cutoffDate);
        
        return deletedCount;
    }
    
    /// <summary>
    /// Process 2-5: CP在庫マスタの当日粗利益・歩引き金額を更新
    /// </summary>
    public async Task<int> UpdateDailyTotalsAsync(DateTime jobDate, string dataSetId, decimal totalGrossProfit, decimal totalDiscountAmount)
    {
        using var connection = CreateConnection();
        
        const string updateSql = @"
            UPDATE CpInventoryMaster 
            SET 
                DailyGrossProfit = DailyGrossProfit + @TotalGrossProfit,
                DailyWalkingAmount = DailyWalkingAmount + @TotalDiscountAmount,
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId";
        
        var updatedCount = await connection.ExecuteAsync(updateSql, new 
        { 
            TotalGrossProfit = totalGrossProfit,
            TotalDiscountAmount = totalDiscountAmount,
            DataSetId = dataSetId
        });
        
        _logger.LogInformation("CP在庫マスタの当日粗利益・歩引き金額を更新しました - 更新件数: {Count}, 粗利益: {GrossProfit}, 歩引き金額: {DiscountAmount}", 
            updatedCount, totalGrossProfit, totalDiscountAmount);
        
        return updatedCount;
    }
    
    /// <summary>
    /// Process 2-5: JobDateとDataSetIdでCP在庫マスタを取得
    /// </summary>
    public async Task<IEnumerable<CpInventoryMaster>> GetByJobDateAndDataSetIdAsync(DateTime jobDate, string dataSetId)
    {
        using var connection = CreateConnection();
        
        const string selectSql = @"
            SELECT * FROM CpInventoryMaster 
            WHERE DataSetId = @DataSetId
            ORDER BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName";
        
        var cpInventories = await connection.QueryAsync<CpInventoryMaster>(selectSql, new 
        { 
            DataSetId = dataSetId
        });
        
        _logger.LogDebug("CP在庫マスタを取得しました - 件数: {Count}, DataSetId: {DataSetId}", 
            cpInventories.Count(), dataSetId);
        
        return cpInventories;
    }
}