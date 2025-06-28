// =====================================
// 在庫マスタ最適化のサンプル実装
// =====================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// 在庫マスタ最適化サービスのサンプル実装
    /// ImportFolderCommandHandlerに統合する処理
    /// </summary>
    public class InventoryMasterOptimizationService
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
        /// 在庫マスタの最適化を実行
        /// CSV取込後に必ず実行する
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
                _logger.LogInformation("売上商品数: {Count}", salesProducts.Count);
                
                // 2. MERGE文で一括処理（推奨）
                var affected = await MergeInventoryMasterAsync(connection, transaction, jobDate, dataSetId);
                result.ProcessedCount = affected;
                
                // 3. 不要なレコードのクリーンアップ（オプション）
                // var cleaned = await CleanupOldRecordsAsync(connection, transaction, jobDate);
                // result.CleanedCount = cleaned;
                
                await transaction.CommitAsync();
                
                _logger.LogInformation(
                    "在庫マスタ最適化完了: 売上商品{Sales}件、処理{Processed}件", 
                    result.SalesProductCount, 
                    result.ProcessedCount);
                
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

        private async Task<int> MergeInventoryMasterAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            DateTime jobDate,
            string dataSetId)
        {
            const string sql = @"
                MERGE InventoryMaster AS target
                USING (
                    SELECT DISTINCT
                        s.ProductCode,
                        s.GradeCode,
                        s.ClassCode,
                        s.ShippingMarkCode,
                        s.ShippingMarkName
                    FROM SalesVouchers s
                    WHERE CONVERT(date, s.JobDate) = @jobDate
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
                    );";
            
            return await connection.ExecuteAsync(
                sql, 
                new { jobDate, dataSetId }, 
                transaction);
        }
    }

    public class OptimizationResult
    {
        public int SalesProductCount { get; set; }
        public int ProcessedCount { get; set; }
        public int CleanedCount { get; set; }
    }

    public class ProductKey
    {
        public string ProductCode { get; set; }
        public string GradeCode { get; set; }
        public string ClassCode { get; set; }
        public string ShippingMarkCode { get; set; }
        public string ShippingMarkName { get; set; }
    }
}

// =====================================
// ImportFolderCommandHandlerへの統合例
// =====================================

// ImportFolderCommandHandler.cs の修正箇所

public async Task<int> ExecuteAsync(string folderPath, DateTime? targetDate = null)
{
    var jobDate = targetDate ?? DateTime.Today;
    var dataSetId = GenerateDataSetId(jobDate);
    
    try
    {
        // 既存のCSV取込処理...
        await ImportSalesVouchersAsync(folderPath, dataSetId, jobDate);
        await ImportPurchaseVouchersAsync(folderPath, dataSetId, jobDate);
        // ... 他のCSV取込

        // ========== ここに追加 ==========
        // 在庫マスタ最適化処理
        _logger.LogInformation("在庫マスタ最適化を開始します");
        
        var optimizationService = new InventoryMasterOptimizationService(
            _connectionString, 
            _loggerFactory.CreateLogger<InventoryMasterOptimizationService>());
        
        var optimizationResult = await optimizationService.OptimizeAsync(jobDate, dataSetId);
        
        _logger.LogInformation(
            "在庫マスタ最適化完了: 売上商品{Sales}件、処理{Processed}件",
            optimizationResult.SalesProductCount,
            optimizationResult.ProcessedCount);
        // ========== ここまで追加 ==========

        // CP在庫マスタ生成処理...
        await GenerateCPInventoryMasterAsync(dataSetId, jobDate);
        
        return 0; // 成功
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "インポート処理でエラーが発生しました");
        throw;
    }
}

// =====================================
// DIコンテナへの登録例
// =====================================

// Program.cs または Startup.cs での登録
services.AddScoped<InventoryMasterOptimizationService>();

// または、既存のサービスに統合する場合
public interface IInventoryService
{
    Task OptimizeInventoryMasterAsync(DateTime jobDate, string dataSetId);
    Task GenerateCPInventoryMasterAsync(string dataSetId, DateTime jobDate);
}

// =====================================
// テスト用のSQL確認クエリ
// =====================================

/*
-- 処理前の確認
SELECT 
    '売上商品' as 種別,
    COUNT(DISTINCT CONCAT(ProductCode,'_',GradeCode,'_',ClassCode,'_',ShippingMarkCode,'_',ShippingMarkName)) as 件数
FROM SalesVouchers
WHERE CONVERT(date, JobDate) = '2025-06-12'
UNION ALL
SELECT 
    '在庫マスタ' as 種別,
    COUNT(*) as 件数
FROM InventoryMaster
WHERE JobDate = '2025-06-12';

-- 処理後の確認（期待値：両方同じ件数）
*/