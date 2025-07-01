using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Services;

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

        public InventoryMasterOptimizationService(
            IConfiguration configuration,
            ILogger<InventoryMasterOptimizationService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("接続文字列が設定されていません");
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
        /// ストアドプロシージャを使用した在庫マスタ最適化（将来対応用）
        /// </summary>
        /// <param name="jobDate">処理対象日</param>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>最適化結果</returns>
        public async Task<OptimizationResult> OptimizeUsingStoredProcedureAsync(DateTime jobDate, string dataSetId)
        {
            var result = new OptimizationResult();
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                _logger.LogInformation("ストアドプロシージャ sp_OptimizeInventoryMaster を実行します");
                
                var parameters = new DynamicParameters();
                parameters.Add("@jobDate", jobDate);
                parameters.Add("@dataSetId", dataSetId ?? "");
                
                // 出力パラメータを追加（ストアドプロシージャが対応している場合）
                parameters.Add("@salesCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("@purchaseCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("@adjustmentCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("@insertedCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("@updatedCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
                
                await connection.ExecuteAsync(
                    "sp_OptimizeInventoryMaster",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 300);
                
                // 結果を取得（ストアドプロシージャが出力パラメータに対応している場合）
                try
                {
                    result.SalesProductCount = parameters.Get<int?>("@salesCount") ?? 0;
                    result.PurchaseProductCount = parameters.Get<int?>("@purchaseCount") ?? 0;
                    result.AdjustmentProductCount = parameters.Get<int?>("@adjustmentCount") ?? 0;
                    result.InsertedCount = parameters.Get<int?>("@insertedCount") ?? 0;
                    result.UpdatedCount = parameters.Get<int?>("@updatedCount") ?? 0;
                    result.ProcessedCount = result.InsertedCount + result.UpdatedCount;
                }
                catch (Exception paramEx)
                {
                    // 出力パラメータが未対応の場合はログのみ出力
                    _logger.LogWarning(paramEx, "ストアドプロシージャの出力パラメータ取得に失敗しました（未対応の可能性）");
                    result.ProcessedCount = 1; // 処理完了とする
                }
                
                _logger.LogInformation("ストアドプロシージャによる在庫マスタ最適化が正常に完了しました");
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, 
                    "ストアドプロシージャ実行でSQLエラーが発生しました。" +
                    "エラーコード: {Number}, 重大度: {Class}, 状態: {State}", 
                    sqlEx.Number, sqlEx.Class, sqlEx.State);
                result.ErrorCount = 1;
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ストアドプロシージャ実行で予期しないエラーが発生しました");
                result.ErrorCount = 1;
                throw;
            }
            
            return result;
        }

        /// <summary>
        /// 在庫マスタの最適化を実行（内部実装）
        /// 売上・仕入・在庫調整で使用される5項目キーの組み合わせを在庫マスタに登録する
        /// </summary>
        /// <param name="jobDate">処理対象日</param>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>最適化結果</returns>
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
                result.PurchaseProductCount = purchaseProducts.Count;
                _logger.LogInformation("仕入商品数: {Count}件", purchaseProducts.Count);
                
                // 3. 在庫調整商品の取得
                var adjustmentProducts = await GetAdjustmentProductsAsync(connection, transaction, jobDate);
                result.AdjustmentProductCount = adjustmentProducts.Count;
                _logger.LogInformation("在庫調整商品数: {Count}件", adjustmentProducts.Count);
                
                // 4. すべての商品を統合（重複除去）
                var allProducts = salesProducts
                    .Union(purchaseProducts)
                    .Union(adjustmentProducts)
                    .Distinct(new ProductKeyComparer())
                    .ToList();
                
                _logger.LogInformation("統合商品数（重複除去後）: {Count}件", allProducts.Count);
                
                // 事前チェック：処理対象データの確認
                if (salesProducts.Count == 0 && purchaseProducts.Count == 0 && adjustmentProducts.Count == 0)
                {
                    _logger.LogWarning("処理対象の伝票データが見つかりません。JobDate: {JobDate}", jobDate);
                    return result;
                }
                
                // 5. MERGE文で一括処理
                var mergeResults = await MergeInventoryMasterAsync(connection, transaction, jobDate, dataSetId);
                result.InsertedCount = mergeResults.insertCount;
                result.UpdatedCount = mergeResults.updateCount;
                result.ProcessedCount = result.InsertedCount + result.UpdatedCount;
                
                await transaction.CommitAsync();
                
                _logger.LogInformation(
                    "在庫マスタ最適化完了: 売上商品{Sales}件、仕入商品{Purchase}件、在庫調整{Adjustment}件、新規{Insert}件、更新{Update}件", 
                    result.SalesProductCount, 
                    result.PurchaseProductCount,
                    result.AdjustmentProductCount,
                    result.InsertedCount,
                    result.UpdatedCount);
                
                return result;
            }
            catch (SqlException sqlEx)
            {
                await transaction.RollbackAsync();
                _logger.LogError(sqlEx, 
                    "在庫マスタ最適化処理でSQLエラーが発生しました。" +
                    "エラーコード: {Number}, 重大度: {Class}, 状態: {State}", 
                    sqlEx.Number, sqlEx.Class, sqlEx.State);
                result.ErrorCount = 1;
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "在庫マスタ最適化で予期しないエラーが発生しました");
                result.ErrorCount = 1;
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
                WHERE CONVERT(date, JobDate) = @jobDate";
            
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
                WHERE CONVERT(date, JobDate) = @jobDate";
            
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
                WHERE CONVERT(date, JobDate) = @jobDate";
            
            var products = await connection.QueryAsync<ProductKey>(
                sql, 
                new { jobDate }, 
                transaction);
            
            return products.ToList();
        }

        private async Task<(int insertCount, int updateCount)> MergeInventoryMasterAsync(
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
                WHEN MATCHED THEN
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
                
            return (insertCount, updateCount);
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