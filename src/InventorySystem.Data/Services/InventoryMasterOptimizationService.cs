using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Data.Services
{
    /// <summary>
    /// 在庫マスタ最適化サービス
    /// CSV取込後に売上商品を在庫マスタに反映する
    /// </summary>
    public class InventoryMasterOptimizationService : IInventoryMasterOptimizationService
    {
        private readonly string _connectionString;
        private readonly ILogger<InventoryMasterOptimizationService> _logger;

        public InventoryMasterOptimizationService(
            string connectionString,
            ILogger<InventoryMasterOptimizationService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// 指定日付の在庫マスタを最適化する
        /// </summary>
        public async Task<int> OptimizeInventoryMasterAsync(DateTime jobDate)
        {
            var dataSetId = $"AUTO_OPTIMIZE_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}";
            var result = await OptimizeAsync(jobDate, dataSetId);
            return result.ProcessedCount;
        }

        /// <summary>
        /// 日付範囲の在庫マスタを最適化する
        /// </summary>
        public async Task<int> OptimizeInventoryMasterRangeAsync(DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("在庫マスタ最適化開始 - 期間: {StartDate} ～ {EndDate}", 
                startDate, endDate);

            var totalCount = 0;
            
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var count = await OptimizeInventoryMasterAsync(date);
                totalCount += count;
            }
            
            _logger.LogInformation("在庫マスタ最適化完了 - 合計{Count}件追加", totalCount);
            return totalCount;
        }

        /// <summary>
        /// 在庫マスタの最適化を実行（内部実装）
        /// </summary>
        public async Task<OptimizationResult> OptimizeAsync(DateTime jobDate, string dataSetId)
        {
            var result = new OptimizationResult();
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var transaction = connection.BeginTransaction();
            try
            {
                _logger.LogInformation("在庫マスタ最適化開始: {JobDate:yyyy-MM-dd}", jobDate);
                
                // 1. 売上商品の取得
                var salesProducts = await GetSalesProductsAsync(connection, transaction, jobDate);
                result.SalesProductCount = salesProducts.Count;
                _logger.LogInformation("売上商品数: {Count}件", salesProducts.Count);
                
                // 2. 仕入商品の取得
                var purchaseProducts = await GetPurchaseProductsAsync(connection, transaction, jobDate);
                _logger.LogInformation("仕入商品数: {Count}件", purchaseProducts.Count);
                
                // 3. 在庫調整商品の取得
                var adjustmentProducts = await GetAdjustmentProductsAsync(connection, transaction, jobDate);
                _logger.LogInformation("在庫調整商品数: {Count}件", adjustmentProducts.Count);
                
                // 4. すべての商品を統合（重複除去）
                var allProducts = salesProducts
                    .Union(purchaseProducts)
                    .Union(adjustmentProducts)
                    .Distinct(new ProductKeyComparer())
                    .ToList();
                
                _logger.LogInformation("統合商品数（重複除去後）: {Count}件", allProducts.Count);
                
                // 5. MERGE文で一括処理
                var affected = await MergeInventoryMasterAsync(connection, transaction, jobDate, dataSetId);
                result.ProcessedCount = affected;
                
                // 6. 対象日の伝票がない商品のJobDateをクリア（90件問題対応）
                var cleaned = await CleanupOldJobDatesAsync(connection, transaction, jobDate);
                result.CleanedCount = cleaned;
                
                await transaction.CommitAsync();
                
                _logger.LogInformation(
                    "在庫マスタ最適化完了: 売上商品{Sales}件、処理{Processed}件、クリーンアップ{Cleaned}件", 
                    result.SalesProductCount, 
                    result.ProcessedCount,
                    result.CleanedCount);
                
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "在庫マスタ最適化エラー");
                throw;
            }
        }

        private async Task<List<ProductKey>> GetSalesProductsAsync(
            SqlConnection connection, 
            SqlTransaction transaction, 
            DateTime jobDate)
        {
            const string sql = @"
                SELECT DISTINCT 
                    ProductCode,
                    GradeCode,
                    ClassCode,
                    ShippingMarkCode,
                    ShippingMarkName
                FROM SalesVouchers
                WHERE CONVERT(date, JobDate) = @jobDate
                    AND Quantity <> 0";
            
            var products = await connection.QueryAsync<ProductKey>(
                sql, 
                new { jobDate }, 
                transaction);
            
            return products.ToList();
        }

        private async Task<List<ProductKey>> GetPurchaseProductsAsync(
            SqlConnection connection, 
            SqlTransaction transaction, 
            DateTime jobDate)
        {
            const string sql = @"
                SELECT DISTINCT 
                    ProductCode,
                    GradeCode,
                    ClassCode,
                    ShippingMarkCode,
                    ShippingMarkName
                FROM PurchaseVouchers
                WHERE CONVERT(date, JobDate) = @jobDate
                    AND Quantity <> 0";
            
            var products = await connection.QueryAsync<ProductKey>(
                sql, 
                new { jobDate }, 
                transaction);
            
            return products.ToList();
        }

        private async Task<List<ProductKey>> GetAdjustmentProductsAsync(
            SqlConnection connection, 
            SqlTransaction transaction, 
            DateTime jobDate)
        {
            const string sql = @"
                SELECT DISTINCT 
                    ProductCode,
                    GradeCode,
                    ClassCode,
                    ShippingMarkCode,
                    ShippingMarkName
                FROM InventoryAdjustments
                WHERE CONVERT(date, JobDate) = @jobDate
                    AND Quantity <> 0";
            
            var products = await connection.QueryAsync<ProductKey>(
                sql, 
                new { jobDate }, 
                transaction);
            
            return products.ToList();
        }

        private async Task<int> MergeInventoryMasterAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            DateTime jobDate,
            string dataSetId)
        {
            // すべての商品のJobDateを対象日付に更新するシンプルなMERGE
            const string sql = @"
                MERGE InventoryMaster AS target
                USING (
                    SELECT DISTINCT
                        ProductCode,
                        GradeCode,
                        ClassCode,
                        ShippingMarkCode,
                        ShippingMarkName
                    FROM (
                        SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                        FROM SalesVouchers
                        WHERE CONVERT(date, JobDate) = @jobDate
                        UNION
                        SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                        FROM PurchaseVouchers
                        WHERE CONVERT(date, JobDate) = @jobDate
                        UNION
                        SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                        FROM InventoryAdjustments
                        WHERE CONVERT(date, JobDate) = @jobDate
                    ) AS products
                ) AS source
                ON target.ProductCode = source.ProductCode
                    AND target.GradeCode = source.GradeCode
                    AND target.ClassCode = source.ClassCode
                    AND target.ShippingMarkCode = source.ShippingMarkCode
                    AND target.ShippingMarkName = source.ShippingMarkName
                WHEN MATCHED AND target.JobDate <> @jobDate THEN
                    UPDATE SET 
                        JobDate = @jobDate,
                        UpdatedDate = GETDATE(),
                        DataSetId = @dataSetId
                WHEN NOT MATCHED THEN
                    INSERT (
                        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                        ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                        JobDate, CreatedDate, UpdatedDate,
                        CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount, DailyFlag,
                        DataSetId, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit
                    )
                    VALUES (
                        source.ProductCode,
                        source.GradeCode,
                        source.ClassCode,
                        source.ShippingMarkCode,
                        source.ShippingMarkName,
                        '商品名未設定',
                        'PCS',
                        0,
                        '',
                        '',
                        @jobDate,
                        GETDATE(),
                        GETDATE(),
                        0, 0, 0, 0, '9',
                        @dataSetId,
                        0, 0, 0, 0
                    )
                OUTPUT $action AS Action;";
            
            var results = await connection.QueryAsync<string>(
                sql, 
                new { jobDate, dataSetId }, 
                transaction);
                
            var insertCount = results.Count(r => r == "INSERT");
            var updateCount = results.Count(r => r == "UPDATE");
            
            _logger.LogInformation(
                "在庫マスタMERGE完了 - 新規作成: {InsertCount}件, 更新: {UpdateCount}件", 
                insertCount, updateCount);
                
            return insertCount + updateCount;
        }

        private async Task<int> CleanupOldJobDatesAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            DateTime jobDate)
        {
            // 対象日の伝票がない商品のJobDateを前日に更新
            const string sql = @"
                UPDATE InventoryMaster
                SET JobDate = DATEADD(day, -1, @jobDate),
                    UpdatedDate = GETDATE()
                WHERE JobDate = @jobDate
                    AND NOT EXISTS (
                        SELECT 1 FROM (
                            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                            FROM SalesVouchers
                            WHERE CONVERT(date, JobDate) = @jobDate
                            UNION
                            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                            FROM PurchaseVouchers
                            WHERE CONVERT(date, JobDate) = @jobDate
                            UNION
                            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                            FROM InventoryAdjustments
                            WHERE CONVERT(date, JobDate) = @jobDate
                        ) AS v
                        WHERE v.ProductCode = InventoryMaster.ProductCode
                            AND v.GradeCode = InventoryMaster.GradeCode
                            AND v.ClassCode = InventoryMaster.ClassCode
                            AND v.ShippingMarkCode = InventoryMaster.ShippingMarkCode
                            AND v.ShippingMarkName = InventoryMaster.ShippingMarkName
                    )";

            return await connection.ExecuteAsync(sql, new { jobDate }, transaction);
        }
    }

    /// <summary>
    /// 最適化結果
    /// </summary>
    public class OptimizationResult
    {
        public int SalesProductCount { get; set; }
        public int ProcessedCount { get; set; }
        public int CleanedCount { get; set; }
    }

    /// <summary>
    /// 商品キー
    /// </summary>
    public class ProductKey
    {
        public string ProductCode { get; set; } = string.Empty;
        public string GradeCode { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public string ShippingMarkCode { get; set; } = string.Empty;
        public string ShippingMarkName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 商品キー比較クラス
    /// </summary>
    public class ProductKeyComparer : IEqualityComparer<ProductKey>
    {
        public bool Equals(ProductKey? x, ProductKey? y)
        {
            if (x == null || y == null)
                return false;

            return x.ProductCode == y.ProductCode
                && x.GradeCode == y.GradeCode
                && x.ClassCode == y.ClassCode
                && x.ShippingMarkCode == y.ShippingMarkCode
                && x.ShippingMarkName == y.ShippingMarkName;
        }

        public int GetHashCode(ProductKey obj)
        {
            return HashCode.Combine(
                obj.ProductCode,
                obj.GradeCode,
                obj.ClassCode,
                obj.ShippingMarkCode,
                obj.ShippingMarkName);
        }
    }
}