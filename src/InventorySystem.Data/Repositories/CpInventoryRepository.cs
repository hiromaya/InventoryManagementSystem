using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Debug;

namespace InventorySystem.Data.Repositories;

public class CpInventoryRepository : BaseRepository, ICpInventoryRepository
{
    private readonly IConfiguration _configuration;

    public CpInventoryRepository(string connectionString, ILogger<CpInventoryRepository> logger, IConfiguration configuration) : base(connectionString, logger)
    {
        _configuration = configuration;
    }

    public async Task<int> CreateCpInventoryFromInventoryMasterAsync(DateTime? jobDate)
    {
        using var connection = new SqlConnection(_connectionString);
        var effectiveDate = jobDate ?? DateTime.Today;
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
            "sp_CreateCpInventoryFromInventoryMasterCumulative",
            new { JobDate = effectiveDate },
            commandType: CommandType.StoredProcedure);

        if (InventoryTracker.IsEnabled)
        {
            await TrackInventoryState("1_CP在庫作成直後 (InventoryMasterベース)", effectiveDate);
        }
        return result?.CreatedCount ?? 0;
    }

    public async Task<int> CreateCpInventoryFromCarryoverAsync(DateTime jobDate)
    {
        // 後方互換API: InventoryMaster版に移行
        return await CreateCpInventoryFromInventoryMasterAsync(jobDate);
    }

    public async Task<int> ClearDailyAreaAsync()
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
                DailyPurchaseDiscountAmount = 0,
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
                -- 当日入荷フラグ初期化
                HasTodayReceipt = 0,
                UpdatedDate = GETDATE()
            -- 仮テーブル設計：全レコード対象
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql);
    }

    public async Task<CpInventoryMaster?> GetByKeyAsync(InventoryKey key)
    {
        const string sql = """
            SELECT * FROM CpInventoryMaster 
            WHERE ProductCode = @ProductCode 
                AND GradeCode = @GradeCode 
                AND ClassCode = @ClassCode 
                AND ShippingMarkCode = @ShippingMarkCode 
                AND ManualShippingMark COLLATE Japanese_CI_AS = @ManualShippingMark COLLATE Japanese_CI_AS
                -- 仮テーブル設計：5項目複合キーで検索
            """;

        using var connection = new SqlConnection(_connectionString);
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new 
        { 
            key.ProductCode, 
            key.GradeCode, 
            key.ClassCode, 
            key.ShippingMarkCode, 
            key.ManualShippingMark
        });

        if (result == null) return null;

        return MapToCpInventoryMaster(result);
    }

    public async Task<IEnumerable<CpInventoryMaster>> GetAllAsync()
    {
        const string sql = "SELECT * FROM CpInventoryMaster -- 仮テーブル設計：全レコード取得";

        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(sql);

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
                DailyIncentiveAmount = @DailyIncentiveAmount, DailyDiscountAmount = @DailyDiscountAmount,
                DailyPurchaseDiscountAmount = @DailyPurchaseDiscountAmount
            WHERE ProductCode = @ProductCode 
                AND GradeCode = @GradeCode 
                AND ClassCode = @ClassCode 
                AND ShippingMarkCode = @ShippingMarkCode 
                AND ManualShippingMark COLLATE Japanese_CI_AS = @ManualShippingMark COLLATE Japanese_CI_AS
                -- 仮テーブル設計：5項目複合キーで更新
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
            cpInventory.DailyPurchaseDiscountAmount,
            cpInventory.Key.ProductCode, cpInventory.Key.GradeCode, cpInventory.Key.ClassCode,
            cpInventory.Key.ShippingMarkCode, cpInventory.Key.ManualShippingMark
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
                AND ManualShippingMark COLLATE Japanese_CI_AS = @ManualShippingMark COLLATE Japanese_CI_AS
                -- 仮テーブル設計：5項目複合キーで更新
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, cpInventories.Select(cp => new
        {
            cp.DailyStock, cp.DailyStockAmount, cp.DailyUnitPrice,
            cp.DailyFlag, cp.UpdatedDate,
            cp.Key.ProductCode, cp.Key.GradeCode, cp.Key.ClassCode,
            cp.Key.ShippingMarkCode, cp.Key.ManualShippingMark
        }));
    }

    public async Task<int> AggregateSalesDataAsync(DateTime? jobDate)
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
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(CASE WHEN DetailType = '1' AND Quantity > 0 THEN Quantity ELSE 0 END) as SalesQuantity,
                    SUM(CASE WHEN DetailType = '1' AND Quantity > 0 THEN Amount ELSE 0 END) as SalesAmount
                FROM SalesVouchers 
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('51', '52')
                    AND DetailType IN ('1', '2')
                    AND ProductCode != '00000'
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) sales ON cp.ProductCode = sales.ProductCode 
                AND cp.GradeCode = sales.GradeCode 
                AND cp.ClassCode = sales.ClassCode 
                AND cp.ShippingMarkCode = sales.ShippingMarkCode
                AND cp.ManualShippingMark COLLATE Japanese_CI_AS = sales.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { JobDate = jobDate });
    }

    public async Task<int> AggregatePurchaseDataAsync(DateTime? jobDate)
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
                -- 仕入入荷があれば当日入荷フラグをON
                HasTodayReceipt = CASE 
                    WHEN purchase.PurchaseQuantity IS NOT NULL AND purchase.PurchaseQuantity > 0 THEN 1
                    ELSE cp.HasTodayReceipt
                END,
                UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(Quantity) as PurchaseQuantity,
                    SUM(Amount) as PurchaseAmount
                FROM PurchaseVouchers 
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('11', '12')
                    AND DetailType IN ('1', '2')  -- 仕入、返品のみ（値引は別途計算）
                    AND Quantity > 0  -- 通常仕入（入荷データ）
                    AND ProductCode != '00000'
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) purchase ON cp.ProductCode = purchase.ProductCode 
                AND cp.GradeCode = purchase.GradeCode 
                AND cp.ClassCode = purchase.ClassCode 
                AND cp.ShippingMarkCode = purchase.ShippingMarkCode
                AND cp.ManualShippingMark COLLATE Japanese_CI_AS = purchase.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象
            """;

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { JobDate = jobDate });
    }

    public async Task<int> AggregateInventoryAdjustmentDataAsync(DateTime? jobDate)
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
                -- 在庫調整入荷（区分1,3,4,6かつ数量>0）があれば当日入荷フラグをON
                HasTodayReceipt = CASE 
                    WHEN adj.AdjustmentQuantity IS NOT NULL AND adj.AdjustmentQuantity > 0 THEN 1
                    ELSE cp.HasTodayReceipt
                END,
                UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(Quantity) as AdjustmentQuantity,
                    SUM(Amount) as AdjustmentAmount
                FROM InventoryAdjustments 
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('71', '72')
                    AND DetailType = '1'  -- 修正: 受注伝票代用のため明細種別1のみ
                    AND CategoryCode IN (1, 3, 6)  -- 在庫調整の単位コード
                    AND Quantity > 0  -- 入荷データ
                    AND ProductCode != '00000'
                    AND IsActive = 1
                    AND DataSetId = (
                        SELECT MAX(DataSetId) FROM InventoryAdjustments 
                        WHERE IsActive = 1 {(jobDate.HasValue ? "AND JobDate = @JobDate" : string.Empty)}
                    )
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) adj ON cp.ProductCode = adj.ProductCode 
                AND cp.GradeCode = adj.GradeCode 
                AND cp.ClassCode = adj.ClassCode 
                AND cp.ShippingMarkCode = adj.ShippingMarkCode
                AND cp.ManualShippingMark COLLATE Japanese_CI_AS = adj.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象
            """;
        var swAdj = System.Diagnostics.Stopwatch.StartNew();
        var adjustmentResult = await connection.ExecuteAsync(adjustmentSql, new { JobDate = jobDate });
        swAdj.Stop();
        _logger.LogInformation("Step Adjust(調整): {Count}件更新, {Elapsed}ms", adjustmentResult, swAdj.ElapsedMilliseconds);
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
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(Quantity) as ProcessingQuantity,
                    SUM(Amount) as ProcessingAmount
                FROM InventoryAdjustments 
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('71', '72')
                    AND DetailType = '1'  -- 修正: 受注伝票代用のため明細種別1のみ
                    AND CategoryCode IN (2, 5)  -- 加工費の単位コード
                    AND Quantity > 0  -- 入荷データ
                    AND ProductCode != '00000'
                    AND IsActive = 1
                    AND DataSetId = (
                        SELECT MAX(DataSetId) FROM InventoryAdjustments 
                        WHERE IsActive = 1 {(jobDate.HasValue ? "AND JobDate = @JobDate" : string.Empty)}
                    )
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) adj ON cp.ProductCode = adj.ProductCode 
                AND cp.GradeCode = adj.GradeCode 
                AND cp.ClassCode = adj.ClassCode 
                AND cp.ShippingMarkCode = adj.ShippingMarkCode
                AND cp.ManualShippingMark COLLATE Japanese_CI_AS = adj.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象
            """;
        var swProc = System.Diagnostics.Stopwatch.StartNew();
        var processingResult = await connection.ExecuteAsync(processingSql, new { JobDate = jobDate });
        swProc.Stop();
        _logger.LogInformation("Step Processing(加工): {Count}件更新, {Elapsed}ms", processingResult, swProc.ElapsedMilliseconds);
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
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(Quantity) as TransferQuantity,
                    SUM(Amount) as TransferAmount
                FROM InventoryAdjustments 
                {dateCondition}
                    {(jobDate.HasValue ? "AND" : "WHERE")} VoucherType IN ('71', '72')
                    AND DetailType = '1'  -- 修正: 受注伝票代用のため明細種別1のみ
                    AND CategoryCode = 4  -- 振替の単位コード
                    AND Quantity > 0  -- 入荷データ
                    AND ProductCode != '00000'
                    AND IsActive = 1
                    AND DataSetId = (
                        SELECT MAX(DataSetId) FROM InventoryAdjustments 
                        WHERE IsActive = 1 {(jobDate.HasValue ? "AND JobDate = @JobDate" : string.Empty)}
                    )
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) adj ON cp.ProductCode = adj.ProductCode 
                AND cp.GradeCode = adj.GradeCode 
                AND cp.ClassCode = adj.ClassCode 
                AND cp.ShippingMarkCode = adj.ShippingMarkCode
                AND cp.ManualShippingMark COLLATE Japanese_CI_AS = adj.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象
            """;
        var swTrans = System.Diagnostics.Stopwatch.StartNew();
        var transferResult = await connection.ExecuteAsync(transferSql, new { JobDate = jobDate });
        swTrans.Stop();
        _logger.LogInformation("Step Transfer(振替): {Count}件更新, {Elapsed}ms", transferResult, swTrans.ElapsedMilliseconds);
        totalUpdated += transferResult;
        
        return totalUpdated;
    }

    public async Task<int> CalculateDailyStockAsync()
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
                
                -- 当日在庫単価の計算（前月末在庫はDailyFlag='9'で判定）
                DailyUnitPrice = CASE
                    -- 前月末在庫（DailyFlag='9'）の場合
                    WHEN DailyFlag = '9' AND PreviousDayStock != 0 
                        THEN ROUND(PreviousDayStockAmount / PreviousDayStock, 4)
                    WHEN DailyFlag = '9' AND PreviousDayStock = 0 
                        THEN 0
                    -- 通常の移動平均法計算
                    WHEN (PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity) = 0 
                        THEN 0
                    ELSE ROUND((PreviousDayStockAmount + DailyPurchaseAmount - DailyPurchaseReturnAmount) /
                               NULLIF(PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity, 0), 4)
                END,
                
                -- 当日在庫金額の計算（前月末在庫はDailyFlag='9'で判定）
                DailyStockAmount = CASE
                    -- 前月末在庫（DailyFlag='9'）の場合
                    WHEN DailyFlag = '9' 
                        THEN PreviousDayStockAmount  -- 前月末在庫金額をそのまま使用
                    -- 通常の計算（当日在庫数量 × 当日在庫単価）
                    ELSE ROUND(
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
                        END, 4)
                END,
                UpdatedDate = GETDATE()
            -- 仮テーブル設計：全レコード対象";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { });
    }

    /// <summary>
    /// 最終入荷日を更新する（バッチ）
    /// 条件:
    ///  - 仕入伝票: VoucherType in ('11','12'), DetailType='1', Quantity>0
    ///  - 在庫調整: VoucherType in ('71','72'), DetailType='1', Quantity>0, CategoryCode in (1,3,4,6)
    /// 除外:
    ///  - 売上返品（51/52かつ明細2）はそもそも対象外
    /// </summary>
    public async Task<int> UpdateLastReceiptDateAsync(DateTime jobDate)
    {
        using var connection = CreateConnection();
        var total = 0;

        // 仕入の入荷で更新
        const string updateFromPurchase = @"
            UPDATE cp
            SET cp.LastReceiptDate = @JobDate,
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            WHERE cp.JobDate = @JobDate
              AND EXISTS (
                SELECT 1 FROM PurchaseVouchers pv
                WHERE pv.JobDate = @JobDate
                  AND pv.VoucherType IN ('11','12')
                  AND pv.DetailType = '1'
                  AND pv.Quantity > 0
                  AND pv.ProductCode = cp.ProductCode
                  AND pv.GradeCode = cp.GradeCode
                  AND pv.ClassCode = cp.ClassCode
                  AND pv.ShippingMarkCode = cp.ShippingMarkCode
                  AND pv.ManualShippingMark = cp.ManualShippingMark
              )";
        total += await connection.ExecuteAsync(updateFromPurchase, new { JobDate = jobDate });

        // 在庫調整の入荷（区分 1,3,4,6）のみで更新（2,5は除外）
        const string updateFromAdjustments = @"
            UPDATE cp
            SET cp.LastReceiptDate = @JobDate,
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            WHERE cp.JobDate = @JobDate
              AND EXISTS (
                SELECT 1 FROM InventoryAdjustments ia
                WHERE ia.JobDate = @JobDate
                  AND ia.VoucherType IN ('71','72')
                  AND ia.DetailType = '1'
                  AND ia.Quantity > 0
                  AND ia.CategoryCode IN (1,3,4,6)
                  AND ia.ProductCode = cp.ProductCode
                  AND ia.GradeCode = cp.GradeCode
                  AND ia.ClassCode = cp.ClassCode
                  AND ia.ShippingMarkCode = cp.ShippingMarkCode
                  AND ia.ManualShippingMark = cp.ManualShippingMark
              )";
        total += await connection.ExecuteAsync(updateFromAdjustments, new { JobDate = jobDate });

        return total;
    }

    /// <summary>
    /// 前日のCarryoverから最終入荷日を補完（cp側に値が無い場合）
    /// </summary>
    public async Task<int> SeedLastReceiptDateFromCarryoverAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET cp.LastReceiptDate = co.LastReceiptDate,
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            CROSS APPLY (
                SELECT TOP 1 c.LastReceiptDate
                FROM InventoryCarryoverMaster c
                WHERE c.ProductCode = cp.ProductCode
                  AND c.GradeCode = cp.GradeCode
                  AND c.ClassCode = cp.ClassCode
                  AND c.ShippingMarkCode = cp.ShippingMarkCode
                  AND c.ManualShippingMark = cp.ManualShippingMark
                  AND c.JobDate < @JobDate
                  AND c.LastReceiptDate IS NOT NULL
                ORDER BY c.JobDate DESC
            ) co
            WHERE cp.JobDate = @JobDate
              AND cp.LastReceiptDate IS NULL";

        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { JobDate = jobDate });
    }

    /// <summary>
    /// 初期在庫（CarryoverMaster）から前日/当日初期在庫を種付け（初日対策）
    /// - 対象: CarryoverMaster.JobDate = @JobDate のスナップショット
    /// - 反映: cp.PreviousDayStock/Amount/UnitPrice と cp.DailyStock/Amount/UnitPrice
    /// </summary>
    public async Task<int> SeedPreviousDayFromCarryoverAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET 
                cp.PreviousDayStock = ISNULL(co.CarryoverQuantity, 0),
                cp.PreviousDayStockAmount = ISNULL(co.CarryoverAmount, 0),
                cp.PreviousDayUnitPrice = CASE 
                    WHEN ISNULL(co.CarryoverQuantity, 0) > 0 AND ISNULL(co.CarryoverAmount, 0) > 0 
                        THEN ROUND(co.CarryoverAmount / NULLIF(co.CarryoverQuantity, 0), 4)
                    ELSE cp.PreviousDayUnitPrice
                END,
                -- 当日の初期値も前日値で初期化（集計後に再計算される）
                cp.DailyStock = ISNULL(co.CarryoverQuantity, cp.DailyStock),
                cp.DailyStockAmount = ISNULL(co.CarryoverAmount, cp.DailyStockAmount),
                cp.DailyUnitPrice = CASE 
                    WHEN ISNULL(co.CarryoverQuantity, 0) > 0 AND ISNULL(co.CarryoverAmount, 0) > 0 
                        THEN ROUND(co.CarryoverAmount / NULLIF(co.CarryoverQuantity, 0), 4)
                    ELSE cp.DailyUnitPrice
                END,
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            INNER JOIN InventoryCarryoverMaster co
                ON co.ProductCode = cp.ProductCode
               AND co.GradeCode = cp.GradeCode
               AND co.ClassCode = cp.ClassCode
               AND co.ShippingMarkCode = cp.ShippingMarkCode
               AND co.ManualShippingMark = cp.ManualShippingMark
               AND co.JobDate = @JobDate;";

        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { JobDate = jobDate });
    }

    public async Task<int> SetDailyFlagToProcessedAsync()
    {
        // このメソッドは使用しないため無効化
        return 0;
    }

    public async Task<int> DeleteAllAsync()
    {
        const string sql = "DELETE FROM CpInventoryMaster -- 仮テーブル設計：全レコード対象";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { });
    }

    public async Task<int> RepairManualShippingMarksAsync()
    {
        const string sql = """
            UPDATE cp
            SET cp.ManualShippingMark = im.ManualShippingMark
            FROM CpInventoryMaster cp
            INNER JOIN InventoryMaster im ON 
                cp.ProductCode = im.ProductCode AND
                cp.GradeCode = im.GradeCode AND
                cp.ClassCode = im.ClassCode AND
                cp.ShippingMarkCode = im.ShippingMarkCode
            -- 仮テーブル設計：全レコード対象
                AND cp.ManualShippingMark LIKE '%?%'
            """;
        
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { });
    }

    public async Task<int> CountGarbledManualShippingMarksAsync()
    {
        const string sql = """
            SELECT COUNT(*)
            FROM CpInventoryMaster
            -- 仮テーブル設計：全レコード対象
            WHERE ManualShippingMark LIKE '%?%'
            """;
        
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, new { });
    }


    public async Task<InventorySystem.Core.Models.AggregationResult> GetAggregationResultAsync()
    {
        const string sql = """
            SELECT 
                COUNT(*) as TotalCount,
                SUM(CASE WHEN DailyFlag = '0' THEN 1 ELSE 0 END) as AggregatedCount,
                SUM(CASE WHEN DailyFlag = '9' THEN 1 ELSE 0 END) as NotAggregatedCount,
                SUM(CASE WHEN DailyFlag = '0' AND DailyPurchaseQuantity = 0 AND DailySalesQuantity = 0 THEN 1 ELSE 0 END) as ZeroTransactionCount
            FROM CpInventoryMaster
            -- 仮テーブル設計：全レコード対象
            """;
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QuerySingleAsync<dynamic>(sql, new { });
            
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
            _logger.LogError(ex, "集計結果取得エラー（仮テーブル処理）");
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
                ManualShippingMark = row.ManualShippingMark ?? string.Empty
            },
            ProductName = row.ProductName ?? string.Empty,
            Unit = row.Unit ?? string.Empty,
            StandardPrice = row.StandardPrice ?? 0m,
            ProductCategory1 = row.ProductCategory1 ?? string.Empty,
            ProductCategory2 = row.ProductCategory2 ?? string.Empty,
            // 名称系（JOINで取得される場合あり）
            ShippingMarkName = row.ShippingMarkName ?? string.Empty,
            GradeName = row.GradeName ?? string.Empty,
            ClassName = row.ClassName ?? string.Empty,
            // 手入力荷印（エンティティ直下プロパティにも設定）
            ManualShippingMark = row.ManualShippingMark ?? string.Empty,
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
            DailyPurchaseDiscountAmount = row.DailyPurchaseDiscountAmount ?? 0m,
            LastReceiptDate = row.LastReceiptDate,
            HasTodayReceipt = row.HasTodayReceipt ?? false,
            // 月計項目
            MonthlySalesQuantity = row.MonthlySalesQuantity ?? 0m,
            MonthlySalesAmount = row.MonthlySalesAmount ?? 0m,
            MonthlySalesReturnQuantity = row.MonthlySalesReturnQuantity ?? 0m,
            MonthlySalesReturnAmount = row.MonthlySalesReturnAmount ?? 0m,
            MonthlyPurchaseQuantity = row.MonthlyPurchaseQuantity ?? 0m,
            MonthlyPurchaseAmount = row.MonthlyPurchaseAmount ?? 0m,
            MonthlyGrossProfit = row.MonthlyGrossProfit ?? 0m,
            MonthlyWalkingAmount = row.MonthlyWalkingAmount ?? 0m,
            MonthlyIncentiveAmount = row.MonthlyIncentiveAmount ?? 0m
        };
    }

    /// <summary>
    /// 当日入荷フラグを設定する
    /// 仕入（11/12）または在庫調整（71/72、区分1/3/4/6）で数量>0の場合にフラグを立てる
    /// </summary>
    public async Task<int> SetHasTodayReceiptFlagAsync(DateTime jobDate)
    {
        const string sql = @"
        UPDATE cp
        SET cp.HasTodayReceipt = 1,
            cp.UpdatedDate = GETDATE()
        FROM CpInventoryMaster cp
        WHERE cp.JobDate = @JobDate
          AND cp.DailyStock != 0
          AND (
            EXISTS (
              SELECT 1 FROM PurchaseVouchers pv
              WHERE pv.JobDate = @JobDate
                AND pv.VoucherType IN ('11','12')
                AND pv.DetailType = '1'
                AND pv.Quantity > 0
                AND pv.ProductCode = cp.ProductCode
                AND pv.GradeCode = cp.GradeCode
                AND pv.ClassCode = cp.ClassCode
                AND pv.ShippingMarkCode = cp.ShippingMarkCode
                AND pv.ManualShippingMark = cp.ManualShippingMark
            )
            OR
            EXISTS (
              SELECT 1 FROM InventoryAdjustments ia
              WHERE ia.JobDate = @JobDate
                AND ia.VoucherType IN ('71','72')
                AND ia.DetailType = '1'
                AND ia.Quantity > 0
                AND ia.CategoryCode IN (1,3,4,6)
                AND ia.ProductCode = cp.ProductCode
                AND ia.GradeCode = cp.GradeCode
                AND ia.ClassCode = cp.ClassCode
                AND ia.ShippingMarkCode = cp.ShippingMarkCode
                AND ia.ManualShippingMark = cp.ManualShippingMark
            )
          )";

        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { JobDate = jobDate });
    }

    /// <summary>
    /// 当日入荷フラグがtrueの商品の最終入荷日を当日に更新する
    /// </summary>
    public async Task<int> UpdateLastReceiptDateByFlagAsync(DateTime jobDate)
    {
        const string sql = @"
        UPDATE CpInventoryMaster
        SET LastReceiptDate = @JobDate,
            UpdatedDate = GETDATE()
        WHERE JobDate = @JobDate
          AND HasTodayReceipt = 1";

        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { JobDate = jobDate });
    }

    /// <summary>
    /// 在庫表用のCP在庫マスタ取得（名称JOIN付）
    /// </summary>
    public async Task<IEnumerable<CpInventoryMaster>> GetInventoryForReportAsync(DateTime jobDate)
    {
        var sql = @"
        SELECT 
            cp.*,
            sm.ShippingMarkName,
            gm.GradeName,
            cm.ClassName
        FROM CpInventoryMaster cp
        LEFT JOIN ShippingMarkMaster sm ON cp.ShippingMarkCode = sm.ShippingMarkCode
        LEFT JOIN GradeMaster gm ON cp.GradeCode = gm.GradeCode  
        LEFT JOIN ClassMaster cm ON cp.ClassCode = cm.ClassCode
        WHERE cp.JobDate = @JobDate
        ORDER BY 
            ISNULL(cp.ProductCategory1, '000'),
            cp.ProductCode,
            cp.ShippingMarkCode,
            cp.ManualShippingMark,
            cp.GradeCode,
            cp.ClassCode";

        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<dynamic>(sql, new { JobDate = jobDate });
        var list = rows.Select(MapToCpInventoryMaster).ToList();

        foreach (var item in list)
        {
            _logger.LogInformation(
                "データ確認: ShippingMarkName={SM}, ManualShippingMark={MS}, GradeName={GN}, ClassName={CN}",
                string.IsNullOrEmpty(item.ShippingMarkName) ? "NULL" : item.ShippingMarkName,
                string.IsNullOrEmpty(item.ManualShippingMark) ? "NULL" : item.ManualShippingMark,
                string.IsNullOrEmpty(item.GradeName) ? "NULL" : item.GradeName,
                string.IsNullOrEmpty(item.ClassName) ? "NULL" : item.ClassName);
        }

        return list;
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
    /// CP在庫マスタの全件数を取得（引数なし版）
    /// </summary>
    /// <returns>レコード件数</returns>
    public async Task<int> GetCountAsync()
    {
        const string sql = "SELECT COUNT(*) FROM CpInventoryMaster";

        using var connection = new SqlConnection(_connectionString);
        var count = await connection.QuerySingleAsync<int>(sql);

        _logger.LogInformation("CP在庫マスタ件数取得: {Count}件", count);
        return count;
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
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(CASE WHEN DetailType = '1' THEN Quantity ELSE 0 END) as Quantity,
                    SUM(CASE WHEN DetailType = '1' THEN Amount ELSE 0 END) as Amount,
                    SUM(CASE WHEN DetailType = '2' THEN Quantity ELSE 0 END) as ReturnQuantity,
                    SUM(CASE WHEN DetailType = '2' THEN Amount ELSE 0 END) as ReturnAmount
                FROM SalesVouchers
                WHERE JobDate >= @monthStartDate AND JobDate <= @jobDate
                    AND VoucherType IN ('51', '52')
                    AND ProductCode != '00000'
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) s ON cp.ProductCode = s.ProductCode 
                AND cp.GradeCode = s.GradeCode 
                AND cp.ClassCode = s.ClassCode 
                AND cp.ShippingMarkCode = s.ShippingMarkCode 
                AND cp.ManualShippingMark = s.ManualShippingMark
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
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(Quantity) as Quantity,
                    SUM(Amount) as Amount
                FROM PurchaseVouchers
                WHERE JobDate >= @monthStartDate AND JobDate <= @jobDate
                    AND VoucherType IN ('11', '12')
                    AND ProductCode != '00000'
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) p ON cp.ProductCode = p.ProductCode 
                AND cp.GradeCode = p.GradeCode 
                AND cp.ClassCode = p.ClassCode 
                AND cp.ShippingMarkCode = p.ShippingMarkCode 
                AND cp.ManualShippingMark = p.ManualShippingMark
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
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(Quantity) as Quantity,
                    SUM(Amount) as Amount
                FROM InventoryAdjustments
                WHERE JobDate >= @monthStartDate AND JobDate <= @jobDate
                    AND VoucherType IN ('71', '72')
                    AND DetailType IN ('1', '3', '4')
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) a ON cp.ProductCode = a.ProductCode 
                AND cp.GradeCode = a.GradeCode 
                AND cp.ClassCode = a.ClassCode 
                AND cp.ShippingMarkCode = a.ShippingMarkCode 
                AND cp.ManualShippingMark = a.ManualShippingMark
            WHERE cp.JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { monthStartDate, jobDate });
    }
    
    /// <summary>
    /// 仕入値引を集計する
    /// </summary>
    public async Task<int> CalculatePurchaseDiscountAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET cp.DailyPurchaseDiscountAmount = ISNULL(pv.DiscountAmount, 0)
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(Amount) as DiscountAmount
                FROM PurchaseVouchers
                WHERE JobDate = @jobDate
                    AND VoucherType IN ('11', '12')
                    AND DetailType = '3'  -- 単品値引
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) pv ON cp.ProductCode = pv.ProductCode 
                AND cp.GradeCode = pv.GradeCode 
                AND cp.ClassCode = pv.ClassCode 
                AND cp.ShippingMarkCode = pv.ShippingMarkCode 
                AND cp.ManualShippingMark = pv.ManualShippingMark
            -- 仮テーブル設計：全レコード対象";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { jobDate });
    }

    /// <summary>
    /// 奨励金を計算する（仕入先分類1='01'の場合、仕入金額の1%）
    /// </summary>
    public async Task<int> CalculateIncentiveAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET cp.DailyIncentiveAmount = ISNULL(pv.IncentiveAmount, 0)
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ManualShippingMark,
                    SUM(CASE WHEN sm.SupplierCategory1 = '01' THEN pv.Amount * 0.01 ELSE 0 END) as IncentiveAmount
                FROM PurchaseVouchers pv
                LEFT JOIN SupplierMaster sm ON pv.SupplierCode = sm.SupplierCode
                WHERE pv.JobDate = @jobDate
                    AND pv.VoucherType IN ('11', '12')
                    AND pv.DetailType IN ('1', '3')  -- 仕入、単品値引
                GROUP BY pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ManualShippingMark
            ) pv ON cp.ProductCode = pv.ProductCode 
                AND cp.GradeCode = pv.GradeCode 
                AND cp.ClassCode = pv.ClassCode 
                AND cp.ShippingMarkCode = pv.ShippingMarkCode 
                AND cp.ManualShippingMark = pv.ManualShippingMark
            -- 仮テーブル設計：全レコード対象";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { jobDate });
    }

    /// <summary>
    /// 歩引き金を計算する（得意先マスタの歩引き率×売上金額）
    /// </summary>
    public async Task<int> CalculateWalkingAmountAsync(DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET cp.DailyWalkingAmount = ISNULL(sv.WalkingAmount, 0)
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ManualShippingMark,
                    SUM(sv.Amount * ISNULL(cm.WalkingRate, 0) / 100) as WalkingAmount
                FROM SalesVouchers sv
                LEFT JOIN CustomerMaster cm ON sv.CustomerCode = cm.CustomerCode
                WHERE sv.JobDate = @jobDate
                    AND sv.VoucherType IN ('51', '52')
                    AND sv.DetailType IN ('1', '2', '3')  -- 売上、返品、単品値引
                GROUP BY sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ManualShippingMark
            ) sv ON cp.ProductCode = sv.ProductCode 
                AND cp.GradeCode = sv.GradeCode 
                AND cp.ClassCode = sv.ClassCode 
                AND cp.ShippingMarkCode = sv.ShippingMarkCode 
                AND cp.ManualShippingMark = sv.ManualShippingMark
            -- 仮テーブル設計：全レコード対象";
        
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new { jobDate });
    }

    /// <summary>
    /// 在庫単価を計算する（移動平均法）
    /// </summary>
    public async Task<int> CalculateInventoryUnitPriceAsync()
    {
        const string sql = @"
            UPDATE CpInventoryMaster
            SET
                -- ①仮在庫数 = 前日在庫数 + 当日入荷数（仕入-仕返）
                --    仕様に基づき、在庫調整（区分1/3/6）、加工費（区分2/5）、振替（区分4）の入荷も含める
                DailyReceiptQuantity = 
                    (DailyPurchaseQuantity - DailyPurchaseReturnQuantity)
                    + DailyInventoryAdjustmentQuantity
                    + DailyTransferQuantity,
                -- ②仮在庫金額 = 前日在庫金額 + 当日入荷金額（在庫調整/加工費/振替の金額を含む）
                DailyReceiptAmount = 
                    (DailyPurchaseAmount - DailyPurchaseReturnAmount)
                    + DailyInventoryAdjustmentAmount
                    + DailyTransferAmount,
                -- ③当日在庫単価 = 仮在庫金額 ÷ 仮在庫数（0除算対策、小数第5位四捨五入）
                DailyUnitPrice = CASE 
                    WHEN (PreviousDayStock + 
                          (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) +
                          DailyInventoryAdjustmentQuantity + DailyTransferQuantity) = 0 THEN 0
                    ELSE ROUND((PreviousDayStockAmount + 
                                (DailyPurchaseAmount - DailyPurchaseReturnAmount) +
                                DailyInventoryAdjustmentAmount + DailyTransferAmount) / 
                               (PreviousDayStock + 
                                (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) +
                                DailyInventoryAdjustmentQuantity + DailyTransferQuantity), 4)
                END,
                -- AveragePrice同期：DailyUnitPriceと同じ値を設定
                AveragePrice = CASE 
                    WHEN (PreviousDayStock + 
                          (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) +
                          DailyInventoryAdjustmentQuantity + DailyTransferQuantity) = 0 THEN 0
                    ELSE ROUND((PreviousDayStockAmount + 
                                (DailyPurchaseAmount - DailyPurchaseReturnAmount) +
                                DailyInventoryAdjustmentAmount + DailyTransferAmount) / 
                               (PreviousDayStock + 
                                (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) +
                                DailyInventoryAdjustmentQuantity + DailyTransferQuantity), 4)
                END,
                -- ④当日在庫数 = 前日在庫数 + 当日入荷数 - 当日出荷数 - 在庫調整 - 加工 - 振替
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
                        WHEN (PreviousDayStock + 
                              (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) +
                              DailyInventoryAdjustmentQuantity + DailyProcessingQuantity + DailyTransferQuantity) = 0 THEN 0
                        ELSE ROUND((PreviousDayStockAmount + 
                                    (DailyPurchaseAmount - DailyPurchaseReturnAmount) +
                                    DailyInventoryAdjustmentAmount + DailyProcessingAmount + DailyTransferAmount) / 
                                   (PreviousDayStock + 
                                    (DailyPurchaseQuantity - DailyPurchaseReturnQuantity) +
                                    DailyInventoryAdjustmentQuantity + DailyProcessingQuantity + DailyTransferQuantity), 4)
                    END, 4),
                UpdatedDate = GETDATE()
            -- 仮テーブル設計：全レコード対象";
        
        using var connection = CreateConnection();
        
        // デバッグ追跡（Process 2-4実行前）
        if (InventoryTracker.IsEnabled)
        {
            await TrackInventoryStateFromCpInventoryMaster("2_Process2-4実行前");
        }
        
        // 在庫単価計算
        var updateCount = await connection.ExecuteAsync(sql, new { });
        
        // デバッグ追跡（Process 2-4実行後）
        if (InventoryTracker.IsEnabled)
        {
            await TrackInventoryStateFromCpInventoryMaster("3_Process2-4実行後");
        }
        
        // 粗利益の調整（在庫調整金額と加工費を減算）
        const string adjustGrossProfitSql = @"
            UPDATE CpInventoryMaster
            SET DailyGrossProfit = DailyGrossProfit - DailyInventoryAdjustmentAmount - DailyProcessingAmount,
                UpdatedDate = GETDATE()
            -- 仮テーブル設計：全レコード対象";
        
        await connection.ExecuteAsync(adjustGrossProfitSql, new { });
        
        // 在庫マスタのAveragePriceを更新（逆同期）
        // const string updateInventoryMasterSql = @"
        //     UPDATE im
        //     SET im.AveragePrice = cp.DailyUnitPrice,
        //         im.UpdatedDate = GETDATE()
        //     FROM InventoryMaster im
        //     INNER JOIN CpInventoryMaster cp ON
        //         im.ProductCode = cp.ProductCode
        //         AND im.GradeCode = cp.GradeCode
        //         AND im.ClassCode = cp.ClassCode
        //         AND im.ShippingMarkCode = cp.ShippingMarkCode
        //         AND im.ManualShippingMark = cp.ManualShippingMark
        //     WHERE cp.DailyUnitPrice > 0";

        // var syncCount = await connection.ExecuteAsync(updateInventoryMasterSql, new { });
        
        _logger.LogInformation(
            "在庫マスタのAveragePriceを更新しました - 更新件数: {UpdateCount}",
            updateCount);
        
        return updateCount;
    }
    
    /// <summary>
    /// 粗利益を計算する（売上伝票1行ごと）
    /// </summary>
    public async Task<int> CalculateGrossProfitAsync(DateTime jobDate)
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
                sv.ManualShippingMark COLLATE Japanese_CI_AS = cp.ManualShippingMark COLLATE Japanese_CI_AS
            WHERE sv.JobDate = @JobDate 
                -- 仮テーブル設計：全レコード対象
                AND sv.VoucherType IN ('51', '52')
                AND sv.DetailType IN ('1', '2', '3')";
        
        await connection.ExecuteAsync(calculateGrossProfitSql, new { JobDate = jobDate });
        
        // Step 3: CP在庫Mの当日粗利益に集計
        const string aggregateGrossProfitSql = @"
            UPDATE cp
            SET cp.DailyGrossProfit = ISNULL(profit.TotalGrossProfit, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(ISNULL(GrossProfit, 0)) as TotalGrossProfit
                FROM SalesVouchers
                WHERE JobDate = @JobDate
                    AND VoucherType IN ('51', '52')
                    AND DetailType IN ('1', '2', '3')
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) profit ON 
                cp.ProductCode = profit.ProductCode AND
                cp.GradeCode = profit.GradeCode AND
                cp.ClassCode = profit.ClassCode AND
                cp.ShippingMarkCode = profit.ShippingMarkCode AND
                cp.ManualShippingMark COLLATE Japanese_CI_AS = profit.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象";
        
        var updateCount = await connection.ExecuteAsync(aggregateGrossProfitSql, new { JobDate = jobDate });
        
        // Step 4: 歩引き金額計算
        const string calculateWalkingAmountSql = @"
            UPDATE cp
            SET cp.DailyWalkingAmount = ISNULL(walk.WalkingAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ManualShippingMark,
                    SUM(ROUND(sv.Amount * ISNULL(c.WalkingRate, 0) / 100, 0)) as WalkingAmount
                FROM SalesVouchers sv
                INNER JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                WHERE sv.JobDate = @JobDate
                    AND sv.VoucherType IN ('51', '52')
                    AND sv.DetailType IN ('1', '2', '3')
                GROUP BY sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ManualShippingMark
            ) walk ON 
                cp.ProductCode = walk.ProductCode AND
                cp.GradeCode = walk.GradeCode AND
                cp.ClassCode = walk.ClassCode AND
                cp.ShippingMarkCode = walk.ShippingMarkCode AND
                cp.ManualShippingMark COLLATE Japanese_CI_AS = walk.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象";
        
        await connection.ExecuteAsync(calculateWalkingAmountSql, new { JobDate = jobDate });
        
        // Step 5: 奨励金計算
        const string calculateIncentiveAmountSql = @"
            UPDATE cp
            SET cp.DailyIncentiveAmount = ISNULL(inc.IncentiveAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ManualShippingMark,
                    SUM(ROUND(pv.Amount * 0.01, 0)) as IncentiveAmount
                FROM PurchaseVouchers pv
                INNER JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                WHERE pv.JobDate = @JobDate
                    AND pv.VoucherType IN ('11', '12')
                    AND pv.DetailType IN ('1', '3')
                    AND s.SupplierCategory1 = '01'
                GROUP BY pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ManualShippingMark
            ) inc ON 
                cp.ProductCode = inc.ProductCode AND
                cp.GradeCode = inc.GradeCode AND
                cp.ClassCode = inc.ClassCode AND
                cp.ShippingMarkCode = inc.ShippingMarkCode AND
                cp.ManualShippingMark COLLATE Japanese_CI_AS = inc.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象";
        
        await connection.ExecuteAsync(calculateIncentiveAmountSql, new { JobDate = jobDate });
        
        // Step 6: 仕入値引き計算は CalculatePurchaseDiscountAsync で実施済みのため削除
        // DailyDiscountAmount への重複設定を回避
        
        return updateCount;
    }
    
    /// <summary>
    /// 前日の在庫マスタから前日在庫を引き継ぐ
    /// </summary>
    public async Task<int> InheritPreviousDayStockAsync(DateTime jobDate, DateTime previousDate)
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
                AND cp.ManualShippingMark COLLATE Japanese_CI_AS = im.ManualShippingMark COLLATE Japanese_CI_AS
                -- 累積管理：JobDateの条件を削除（最新の在庫情報を使用）
            -- 仮テーブル設計：全レコード対象;
            
            -- DailyStockも前日在庫で初期化（後の集計処理で正しい値に更新される）
            UPDATE CpInventoryMaster
            SET DailyStock = PreviousDayStock,
                DailyStockAmount = PreviousDayStockAmount,
                DailyUnitPrice = PreviousDayUnitPrice,
                UpdatedDate = GETDATE()
            -- 仮テーブル設計：全レコード対象;";

        return await connection.ExecuteAsync(sql, new 
        { 
            PreviousDate = previousDate  // パラメータは保持するが使用しない（後方互換性のため）
        });
    }

    /// <summary>
    /// 月計合計を計算する
    /// </summary>
    public async Task<int> CalculateMonthlyTotalsAsync(DateTime jobDate)
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
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    SUM(ISNULL(GrossProfit, 0)) as TotalGrossProfit
                FROM SalesVouchers
                WHERE JobDate >= @MonthStartDate AND JobDate <= @JobDate
                    AND VoucherType IN ('51', '52')
                    AND DetailType IN ('1', '2', '3')
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
            ) profit ON 
                cp.ProductCode = profit.ProductCode AND
                cp.GradeCode = profit.GradeCode AND
                cp.ClassCode = profit.ClassCode AND
                cp.ShippingMarkCode = profit.ShippingMarkCode AND
                cp.ManualShippingMark COLLATE Japanese_CI_AS = profit.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象";
        
        var updateCount = await connection.ExecuteAsync(calculateMonthlyGrossProfitSql, 
            new { MonthStartDate = monthStartDate, JobDate = jobDate });
        
        // 月計歩引き金額計算
        const string calculateMonthlyWalkingAmountSql = @"
            UPDATE cp
            SET cp.MonthlyWalkingAmount = ISNULL(walk.WalkingAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ManualShippingMark,
                    SUM(ROUND(sv.Amount * ISNULL(c.WalkingRate, 0) / 100, 0)) as WalkingAmount
                FROM SalesVouchers sv
                INNER JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                WHERE sv.JobDate >= @MonthStartDate AND sv.JobDate <= @JobDate
                    AND sv.VoucherType IN ('51', '52')
                    AND sv.DetailType IN ('1', '2', '3')
                GROUP BY sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ManualShippingMark
            ) walk ON 
                cp.ProductCode = walk.ProductCode AND
                cp.GradeCode = walk.GradeCode AND
                cp.ClassCode = walk.ClassCode AND
                cp.ShippingMarkCode = walk.ShippingMarkCode AND
                cp.ManualShippingMark COLLATE Japanese_CI_AS = walk.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象";
        
        await connection.ExecuteAsync(calculateMonthlyWalkingAmountSql, 
            new { MonthStartDate = monthStartDate, JobDate = jobDate });
        
        // 月計奨励金計算
        const string calculateMonthlyIncentiveAmountSql = @"
            UPDATE cp
            SET cp.MonthlyIncentiveAmount = ISNULL(inc.IncentiveAmount, 0),
                cp.UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN (
                SELECT 
                    pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ManualShippingMark,
                    SUM(ROUND(pv.Amount * 0.01, 0)) as IncentiveAmount
                FROM PurchaseVouchers pv
                INNER JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                WHERE pv.JobDate >= @MonthStartDate AND pv.JobDate <= @JobDate
                    AND pv.VoucherType IN ('11', '12')
                    AND pv.DetailType IN ('1', '3')
                    AND s.SupplierCategory1 = '01'
                GROUP BY pv.ProductCode, pv.GradeCode, pv.ClassCode, pv.ShippingMarkCode, pv.ManualShippingMark
            ) inc ON 
                cp.ProductCode = inc.ProductCode AND
                cp.GradeCode = inc.GradeCode AND
                cp.ClassCode = inc.ClassCode AND
                cp.ShippingMarkCode = inc.ShippingMarkCode AND
                cp.ManualShippingMark COLLATE Japanese_CI_AS = inc.ManualShippingMark COLLATE Japanese_CI_AS
            -- 仮テーブル設計：全レコード対象";
        
        await connection.ExecuteAsync(calculateMonthlyIncentiveAmountSql, 
            new { MonthStartDate = monthStartDate, JobDate = jobDate });
        
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
    
    // 削除: UpdateDailyTotalsAsyncメソッド
    // 修正理由: 全CP在庫レコードに同じ総計値を加算してしまう問題があるため削除
    // 個別商品の粗利益集計はCalculateGrossProfitAsyncで正しく実行される
    
    /// <summary>
    /// Process 2-5: JobDateでCP在庫マスタを取得（仮テーブル設計）
    /// </summary>
    public async Task<IEnumerable<CpInventoryMaster>> GetByJobDateAsync(DateTime jobDate)
    {
        const string selectSql = @"
            SELECT * FROM CpInventoryMaster 
            -- 仮テーブル設計：全レコード対象 
            WHERE JobDate = @JobDate
            ORDER BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var cpInventories = await connection.QueryAsync<dynamic>(selectSql, new 
            { 
                    JobDate = jobDate  // JobDateパラメータを追加
            });

            var result = cpInventories.Select(MapToCpInventoryMaster).ToList();
            
            _logger.LogInformation(
                "CP在庫マスタ取得完了: JobDate={JobDate}, 件数={Count}", 
                jobDate, result.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "CP在庫マスタ取得エラー: JobDate={JobDate}", 
                jobDate);
            throw;
        }
    }

    /// <summary>
    /// デバッグ用：在庫状態を追跡（独自の接続を使用）
    /// </summary>
    private async Task TrackInventoryState(string processName, DateTime jobDate)
    {
        try
        {
            // 新しい接続を作成
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            var query = @"
                SELECT 
                    cp.ProductCode,
                    cp.GradeCode,
                    cp.ClassCode,
                    cp.ShippingMarkCode,
                    cp.ManualShippingMark,
                    cp.PreviousDayUnitPrice,
                    cp.DailyUnitPrice,
                    cp.StandardPrice,
                    cp.AveragePrice,
                    cp.PreviousDayStock,
                    cp.PreviousDayStockAmount,
                    cp.DailyPurchaseQuantity,
                    cp.DailyPurchaseAmount,
                    cp.DailySalesQuantity,
                    ISNULL(sv.UnitPrice, 0) as SalesUnitPrice,
                    ISNULL(sv.VoucherNumber, '') as VoucherNumber
                FROM CpInventoryMaster cp
                LEFT JOIN (
                    SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode,
                           MAX(UnitPrice) as UnitPrice,
                           MAX(VoucherNumber) as VoucherNumber
                    FROM SalesVouchers
                    WHERE JobDate = @JobDate
                    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode
                    -- ManualShippingMarkは使用しない（4項目マッチング）
                ) sv ON cp.ProductCode = sv.ProductCode
                    AND cp.GradeCode = sv.GradeCode
                    AND cp.ClassCode = sv.ClassCode
                    AND cp.ShippingMarkCode = sv.ShippingMarkCode
                WHERE cp.JobDate = @JobDate
                  AND (@PCode IS NULL OR cp.ProductCode = @PCode)
                  AND (@GCode IS NULL OR cp.GradeCode = @GCode)
                  AND (@CCode IS NULL OR cp.ClassCode = @CCode)
                  AND (@SCode IS NULL OR cp.ShippingMarkCode = @SCode)";
                    
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@JobDate", jobDate);
            var key = InventorySystem.Core.Debug.InventoryTracker.GetTrackingKey();
            cmd.Parameters.AddWithValue("@PCode", (object?)key.ProductCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GCode", (object?)key.GradeCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CCode", (object?)key.ClassCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SCode", (object?)key.ShippingMarkCode ?? DBNull.Value);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var data = new InventoryTrackingData
                {
                    ProductCode = reader["ProductCode"].ToString() ?? "",
                    GradeCode = reader["GradeCode"].ToString() ?? "",
                    ClassCode = reader["ClassCode"].ToString() ?? "",
                    ShippingMarkCode = reader["ShippingMarkCode"].ToString() ?? "",
                    ManualShippingMark = reader["ManualShippingMark"].ToString() ?? "",
                    PreviousDayUnitPrice = Convert.ToDecimal(reader["PreviousDayUnitPrice"]),
                    DailyUnitPrice = Convert.ToDecimal(reader["DailyUnitPrice"]),
                    StandardPrice = Convert.ToDecimal(reader["StandardPrice"]),
                    AveragePrice = Convert.ToDecimal(reader["AveragePrice"]),
                    PreviousDayStock = Convert.ToDecimal(reader["PreviousDayStock"]),
                    PreviousDayStockAmount = Convert.ToDecimal(reader["PreviousDayStockAmount"]),
                    DailyPurchaseQuantity = Convert.ToDecimal(reader["DailyPurchaseQuantity"]),
                    DailyPurchaseAmount = Convert.ToDecimal(reader["DailyPurchaseAmount"]),
                    DailySalesQuantity = Convert.ToDecimal(reader["DailySalesQuantity"]),
                    SalesUnitPrice = Convert.ToDecimal(reader["SalesUnitPrice"]),
                    VoucherNumber = reader["VoucherNumber"].ToString() ?? ""
                };
                
                InventoryTracker.Track(processName, data);
                
                var titleKey = key.ShippingMarkCode == null ?
                    $"{key.ProductCode}-{key.GradeCode}-{key.ClassCode}" :
                    $"{key.ProductCode}-{key.GradeCode}-{key.ClassCode}-{key.ShippingMarkCode}";
                _logger.LogInformation(
                    $"[{processName}] {titleKey}: " +
                    $"CP当日単価={data.DailyUnitPrice:N2}, " +
                    $"売上単価={data.SalesUnitPrice:N2}, " +
                    $"前日在庫={data.PreviousDayStock:N2}, " +
                    $"前日金額={data.PreviousDayStockAmount:N2}, " +
                    $"診断={InventoryTracker.GetDiagnosis(data)}");
            }
            else
            {
                var titleKey = InventorySystem.Core.Debug.InventoryTracker.GetTrackingKey();
                var keyText = titleKey.ShippingMarkCode == null ?
                    $"{titleKey.ProductCode}-{titleKey.GradeCode}-{titleKey.ClassCode}" :
                    $"{titleKey.ProductCode}-{titleKey.GradeCode}-{titleKey.ClassCode}-{titleKey.ShippingMarkCode}";
                _logger.LogWarning($"[{processName}] {keyText}: データが見つかりません");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"TrackInventoryState エラー: {processName}");
            // デバッグ処理のエラーは本処理に影響しないようにする
        }
    }

    /// <summary>
    /// デバッグ用：在庫状態を追跡（CP在庫マスタから日付を自動取得）
    /// </summary>
    private async Task TrackInventoryStateFromCpInventoryMaster(string processName)
    {
        try
        {
            // 新しい接続を作成してJobDateを取得
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // CP在庫マスタから最新のJobDateを取得
            var jobDateQuery = @"
                SELECT TOP 1 JobDate 
                FROM CpInventoryMaster 
                WHERE (@PCode IS NULL OR ProductCode = @PCode)
                  AND (@GCode IS NULL OR GradeCode = @GCode)
                  AND (@CCode IS NULL OR ClassCode = @CCode)
                  AND (@SCode IS NULL OR ShippingMarkCode = @SCode)
                ORDER BY JobDate DESC";

            var key = InventorySystem.Core.Debug.InventoryTracker.GetTrackingKey();
            var jobDate = await connection.QueryFirstOrDefaultAsync<DateTime?>(jobDateQuery, new {
                PCode = (object?)key.ProductCode ?? DBNull.Value,
                GCode = (object?)key.GradeCode ?? DBNull.Value,
                CCode = (object?)key.ClassCode ?? DBNull.Value,
                SCode = (object?)key.ShippingMarkCode ?? DBNull.Value
            });
            if (jobDate.HasValue)
            {
                await TrackInventoryState(processName, jobDate.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"TrackInventoryStateFromCpInventoryMaster エラー: {processName}");
        }
    }

    public async Task<int> UpdateMonthlyTotalsByProductCode(
        DateTime jobDate,
        string productCode,
        decimal monthlySalesAmount,
        decimal monthlySalesReturnAmount,
        decimal monthlyGrossProfit1,
        decimal monthlyGrossProfit2,
        decimal monthlyWalkingAmount)
    {
        const string sql = @"
            UPDATE CpInventoryMaster
            SET MonthlySalesAmount = @MonthlySalesAmount,
                MonthlySalesReturnAmount = @MonthlySalesReturnAmount,
                MonthlyGrossProfit = @MonthlyGrossProfit1,
                MonthlyWalkingAmount = @MonthlyWalkingAmount,
                UpdatedDate = GETDATE()
            WHERE JobDate = @JobDate
              AND ProductCode = @ProductCode";

        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, new
        {
            JobDate = jobDate,
            ProductCode = productCode,
            MonthlySalesAmount = monthlySalesAmount,
            MonthlySalesReturnAmount = monthlySalesReturnAmount,
            MonthlyGrossProfit1 = monthlyGrossProfit1,
            MonthlyGrossProfit2 = monthlyGrossProfit2,
            MonthlyWalkingAmount = monthlyWalkingAmount
        });
    }
}
