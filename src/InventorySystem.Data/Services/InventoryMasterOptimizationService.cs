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
            // Gemini推奨のランダム文字列を含むDataSetId
            var random = GenerateRandomString(6);
            var dataSetId = $"IMPORT_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}_{random}";
            
            // 月初の場合は前月末在庫処理を追加
            if (jobDate.Day == 1)
            {
                await HandleMonthStartInventoryAsync(jobDate);
            }
            
            var result = await OptimizeAsync(jobDate, dataSetId);
            return result.ProcessedCount;
        }
        
        /// <summary>
        /// ランダム文字列を生成する
        /// </summary>
        private static string GenerateRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// 月初の在庫処理（前月末在庫を考慮）
        /// </summary>
        private async Task HandleMonthStartInventoryAsync(DateTime jobDate)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = await connection.BeginTransactionAsync();

                const string sql = @"
                    -- 前月末在庫データを当月1日の前日在庫として設定
                    UPDATE im
                    SET 
                        PreviousMonthQuantity = pmi.Quantity,
                        PreviousMonthAmount = pmi.Amount,
                        CurrentStock = pmi.Quantity,
                        CurrentStockAmount = pmi.Amount,
                        DailyStock = pmi.Quantity,
                        DailyStockAmount = pmi.Amount,
                        UpdatedDate = GETDATE()
                    FROM InventoryMaster im
                    INNER JOIN PreviousMonthInventory pmi
                        ON im.ProductCode = pmi.ProductCode
                        AND im.GradeCode = pmi.GradeCode
                        AND im.ClassCode = pmi.ClassCode
                        AND im.ShippingMarkCode = pmi.ShippingMarkCode
                        AND LEFT(RTRIM(COALESCE(im.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = LEFT(RTRIM(COALESCE(pmi.ShippingMarkName, '')) + REPLICATE(' ', 8), 8)
                    WHERE CAST(im.JobDate AS DATE) = CAST(@JobDate AS DATE)
                        AND CAST(pmi.JobDate AS DATE) = CAST(@JobDate AS DATE);
                    
                    -- 前月末在庫のみ存在する商品を新規追加
                    INSERT INTO InventoryMaster (
                        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                        ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                        JobDate, CreatedDate, UpdatedDate,
                        CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount, DailyFlag,
                        PreviousMonthQuantity, PreviousMonthAmount
                    )
                    SELECT 
                        pmi.ProductCode, pmi.GradeCode, pmi.ClassCode, 
                        pmi.ShippingMarkCode, LEFT(RTRIM(COALESCE(pmi.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName,
                        COALESCE(pm.ProductName, '商' + pmi.ProductCode) as ProductName,
                        COALESCE(pm.UnitCode, 'PCS') as Unit,
                        COALESCE(pm.StandardPrice, 0) as StandardPrice,
                        COALESCE(pm.ProductCategory1, '') as ProductCategory1,
                        COALESCE(pm.ProductCategory2, '') as ProductCategory2,
                        @JobDate, GETDATE(), GETDATE(),
                        pmi.Quantity, pmi.Amount,  -- 当日在庫（初期値は前月末と同じ）
                        pmi.Quantity, pmi.Amount,  -- 日次在庫
                        '9',
                        pmi.Quantity, pmi.Amount  -- 前月末在庫
                    FROM PreviousMonthInventory pmi
                    LEFT JOIN ProductMaster pm ON pm.ProductCode = pmi.ProductCode
                    WHERE CAST(pmi.JobDate AS DATE) = CAST(@JobDate AS DATE)
                        AND NOT EXISTS (
                            SELECT 1 FROM InventoryMaster im
                            WHERE im.ProductCode = pmi.ProductCode
                                AND im.GradeCode = pmi.GradeCode
                                AND im.ClassCode = pmi.ClassCode
                                AND im.ShippingMarkCode = pmi.ShippingMarkCode
                                AND LEFT(RTRIM(COALESCE(im.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = LEFT(RTRIM(COALESCE(pmi.ShippingMarkName, '')) + REPLICATE(' ', 8), 8)
                                AND CAST(im.JobDate AS DATE) = CAST(@JobDate AS DATE)
                        );";

                await connection.ExecuteAsync(sql, new { JobDate = jobDate }, transaction);
                await transaction.CommitAsync();
                
                _logger.LogInformation("月初在庫処理完了: JobDate={JobDate}", jobDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "月初在庫処理エラー: JobDate={JobDate}", jobDate);
                throw;
            }
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
                _logger.LogInformation("在庫マスタ最適化開始: JobDate={JobDate:yyyy-MM-dd}, DataSetId={DataSetId}", 
                    jobDate, dataSetId);
                
                // デバッグログ追加: 売上伝票検索条件
                _logger.LogDebug("売上伝票検索条件: CAST(JobDate AS DATE) = CAST('{JobDate:yyyy-MM-dd}' AS DATE)", jobDate);
                
                // SQLパラメータ確認
                _logger.LogDebug("SQLパラメータ: @jobDate={JobDate}, Type={Type}", jobDate, jobDate.GetType().Name);
                
                // 1. 売上商品の取得
                var salesProducts = await GetSalesProductsAsync(connection, transaction, jobDate);
                result.SalesProductCount = salesProducts.Count;
                _logger.LogInformation("売上商品数: {Count}件", salesProducts.Count);
                
                // デバッグログ追加: 売上商品が0件の場合の詳細確認
                if (salesProducts.Count == 0)
                {
                    var allDates = await connection.QueryAsync<DateTime>(
                        "SELECT DISTINCT CAST(JobDate AS DATE) as JobDate FROM SalesVouchers ORDER BY JobDate DESC", 
                        null, transaction);
                    _logger.LogWarning("売上伝票の全JobDate: {Dates}", 
                        string.Join(", ", allDates.Select(d => d.ToString("yyyy-MM-dd"))));
                        
                    var recentCount = await connection.QuerySingleAsync<int>(
                        "SELECT COUNT(*) FROM SalesVouchers WHERE CreatedDate >= DATEADD(day, -1, GETDATE())",
                        null, transaction);
                    _logger.LogWarning("直近24時間内の売上伝票件数: {Count}件", recentCount);
                }
                
                // 2. 仕入商品の取得
                _logger.LogDebug("仕入伝票検索条件: CAST(JobDate AS DATE) = CAST('{JobDate:yyyy-MM-dd}' AS DATE)", jobDate);
                var purchaseProducts = await GetPurchaseProductsAsync(connection, transaction, jobDate);
                result.PurchaseProductCount = purchaseProducts.Count;
                _logger.LogInformation("仕入商品数: {Count}件", purchaseProducts.Count);
                
                if (purchaseProducts.Count == 0)
                {
                    var allDates = await connection.QueryAsync<DateTime>(
                        "SELECT DISTINCT CAST(JobDate AS DATE) as JobDate FROM PurchaseVouchers ORDER BY JobDate DESC", 
                        null, transaction);
                    _logger.LogWarning("仕入伝票の全JobDate: {Dates}", 
                        string.Join(", ", allDates.Select(d => d.ToString("yyyy-MM-dd"))));
                }
                
                // 3. 在庫調整商品の取得
                _logger.LogDebug("在庫調整検索条件: CAST(JobDate AS DATE) = CAST('{JobDate:yyyy-MM-dd}' AS DATE)", jobDate);
                var adjustmentProducts = await GetAdjustmentProductsAsync(connection, transaction, jobDate);
                result.AdjustmentProductCount = adjustmentProducts.Count;
                _logger.LogInformation("在庫調整商品数: {Count}件", adjustmentProducts.Count);
                
                if (adjustmentProducts.Count == 0)
                {
                    var allDates = await connection.QueryAsync<DateTime>(
                        "SELECT DISTINCT CAST(JobDate AS DATE) as JobDate FROM InventoryAdjustments ORDER BY JobDate DESC", 
                        null, transaction);
                    _logger.LogWarning("在庫調整の全JobDate: {Dates}", 
                        string.Join(", ", allDates.Select(d => d.ToString("yyyy-MM-dd"))));
                }
                
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
                
                // 4.5. 前日在庫の引き継ぎ処理は削除（スナップショット管理のため不要）
                // 主キーが5項目になったため、日付別の履歴管理は行わない
                _logger.LogInformation("スナップショット管理モデルのため、前日引き継ぎ処理はスキップします");
                
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
                    LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName
                FROM SalesVouchers
                WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)";
            
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
                    LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName
                FROM PurchaseVouchers
                WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)";
            
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
                    LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName
                FROM InventoryAdjustments
                WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)";
            
            var products = await connection.QueryAsync<ProductKey>(
                sql, 
                new { jobDate }, 
                transaction);
            
            return products.ToList();
        }

        /// <summary>
        /// 前日在庫を当日に引き継ぐ処理（スナップショット管理モデルのため削除）
        /// </summary>
        /// <param name="connection">データベース接続</param>
        /// <param name="transaction">トランザクション</param>
        /// <param name="jobDate">当日日付</param>
        /// <returns>引き継いだ在庫件数</returns>
        [Obsolete("スナップショット管理モデルに移行したため、このメソッドは使用されません")]
        private async Task<int> InheritPreviousDayInventoryAsync(
            SqlConnection connection, 
            SqlTransaction transaction, 
            DateTime jobDate)
        {
            var previousDate = jobDate.AddDays(-1);
            
            _logger.LogInformation("前日在庫引き継ぎ開始: 前日={PreviousDate:yyyy-MM-dd}, 当日={JobDate:yyyy-MM-dd}", 
                previousDate, jobDate);
            
            const string inheritSql = @"
                -- 前日の在庫マスタを当日にコピー（CurrentStockを引き継ぎ）
                INSERT INTO InventoryMaster (
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                    JobDate, CreatedDate, UpdatedDate,
                    CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount, DailyFlag,
                    PreviousMonthQuantity, PreviousMonthAmount
                )
                SELECT 
                    prev.ProductCode, prev.GradeCode, prev.ClassCode, 
                    prev.ShippingMarkCode, 
                    LEFT(RTRIM(COALESCE(prev.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName,
                    prev.ProductName, prev.Unit, prev.StandardPrice, 
                    prev.ProductCategory1, prev.ProductCategory2,
                    @JobDate, GETDATE(), GETDATE(),
                    prev.CurrentStock, prev.CurrentStockAmount,  -- 前日在庫を引き継ぎ
                    prev.CurrentStock, prev.CurrentStockAmount,  -- 日次在庫も初期値として設定
                    '9',  -- 未処理フラグ
                    prev.PreviousMonthQuantity, prev.PreviousMonthAmount
                FROM InventoryMaster prev
                WHERE CAST(prev.JobDate AS DATE) = CAST(@PreviousDate AS DATE)
                    AND NOT EXISTS (
                        -- 当日のデータが既に存在する場合はスキップ（月初処理との重複回避）
                        SELECT 1 FROM InventoryMaster curr
                        WHERE curr.ProductCode = prev.ProductCode
                            AND curr.GradeCode = prev.GradeCode
                            AND curr.ClassCode = prev.ClassCode
                            AND curr.ShippingMarkCode = prev.ShippingMarkCode
                            AND LEFT(RTRIM(COALESCE(curr.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = 
                                LEFT(RTRIM(COALESCE(prev.ShippingMarkName, '')) + REPLICATE(' ', 8), 8)
                            AND CAST(curr.JobDate AS DATE) = CAST(@JobDate AS DATE)
                    );";
            
            var inheritedCount = await connection.ExecuteAsync(inheritSql, 
                new { JobDate = jobDate, PreviousDate = previousDate }, 
                transaction);
            
            _logger.LogInformation("前日在庫引き継ぎ完了: {Count}件（前日={PreviousDate:yyyy-MM-dd}→当日={JobDate:yyyy-MM-dd}）", 
                inheritedCount, previousDate, jobDate);
            
            return inheritedCount;
        }

        private async Task<(int insertCount, int updateCount)> MergeInventoryMasterAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            DateTime jobDate,
            string dataSetId)
        {
            try
            {
                // スナップショット管理用ストアドプロシージャを呼び出す
                var result = await connection.QuerySingleAsync<dynamic>(
                    "sp_MergeInventoryMasterSnapshot",
                    new { JobDate = jobDate, DataSetId = dataSetId },
                    transaction,
                    commandType: CommandType.StoredProcedure);
                
                var insertCount = (int)(result.InsertedCount ?? 0);
                var updateCount = (int)(result.UpdatedCount ?? 0);
                
                _logger.LogInformation(
                    "在庫マスタMERGE完了: 新規={InsertCount}件, 更新={UpdateCount}件",
                    insertCount, updateCount);
                
                // 既存の「商品名未設定」データを修正
                var fixedCount = await FixProductNamesAsync(connection, transaction);
                if (fixedCount > 0)
                {
                    _logger.LogInformation("「商品名未設定」データを修正しました: {Count}件", fixedCount);
                }
                
                return (insertCount, updateCount);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, 
                    "在庫マスタのMERGE処理でエラーが発生しました。" +
                    "JobDate={JobDate}, DataSetId={DataSetId}", 
                    jobDate, dataSetId);
                throw;
            }
        }

        private async Task<int> FixProductNamesAsync(
            SqlConnection connection,
            SqlTransaction transaction)
        {
            // 既存の「商品名未設定」データを商品マスタから正しい商品名に修正
            const string sql = @"
                UPDATE im
                SET im.ProductName = COALESCE(pm.ProductName, '商' + im.ProductCode),
                    im.Unit = COALESCE(pm.UnitCode, im.Unit),
                    im.StandardPrice = COALESCE(pm.StandardPrice, im.StandardPrice),
                    im.ProductCategory1 = COALESCE(pm.ProductCategory1, im.ProductCategory1),
                    im.ProductCategory2 = COALESCE(pm.ProductCategory2, im.ProductCategory2),
                    im.UpdatedDate = GETDATE()
                FROM InventoryMaster im
                LEFT JOIN ProductMaster pm ON pm.ProductCode = im.ProductCode
                WHERE im.ProductName = '商品名未設定'";

            return await connection.ExecuteAsync(sql, transaction: transaction);
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
                            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, 
                                   LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName
                            FROM SalesVouchers
                            WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)
                            UNION
                            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, 
                                   LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName
                            FROM PurchaseVouchers
                            WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)
                            UNION
                            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, 
                                   LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName
                            FROM InventoryAdjustments
                            WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)
                        ) AS v
                        WHERE v.ProductCode = InventoryMaster.ProductCode
                            AND v.GradeCode = InventoryMaster.GradeCode
                            AND v.ClassCode = InventoryMaster.ClassCode
                            AND v.ShippingMarkCode = InventoryMaster.ShippingMarkCode
                            AND v.ShippingMarkName = LEFT(RTRIM(COALESCE(InventoryMaster.ShippingMarkName, '')) + REPLICATE(' ', 8), 8)
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