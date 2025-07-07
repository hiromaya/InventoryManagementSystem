using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Dapper;

namespace InventorySystem.Data.Services;

/// <summary>
/// データベーススキーマの自動更新サービス
/// </summary>
public class DatabaseSchemaService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseSchemaService> _logger;

    public DatabaseSchemaService(string connectionString, ILogger<DatabaseSchemaService> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// スキーマを最新版に更新
    /// </summary>
    public async Task UpdateSchemaAsync()
    {
        _logger.LogInformation("データベーススキーマの確認を開始します...");

        try
        {
            await AddGrossProfitColumnsAsync();
            await UpdateVoucherIdSizeAsync();
            await AddProductNameColumnAsync();
            await AddMonthlyColumnsAsync();
            await CreateStoredProceduresAsync();
            
            _logger.LogInformation("データベーススキーマの更新が完了しました。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データベーススキーマの更新中にエラーが発生しました。");
            throw;
        }
    }

    /// <summary>
    /// GrossProfitカラムの追加
    /// </summary>
    private async Task AddGrossProfitColumnsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        // SalesVouchersテーブル
        var salesGrossProfitExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
            AND name = 'GrossProfit'") > 0;

        if (!salesGrossProfitExists)
        {
            _logger.LogInformation("SalesVouchersテーブルにGrossProfitカラムを追加します...");
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[SalesVouchers]
                ADD [GrossProfit] DECIMAL(16,4) NULL");
            _logger.LogInformation("GrossProfitカラムを追加しました。");
        }

        // PurchaseVouchersテーブル
        var purchaseGrossProfitExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseVouchers]') 
            AND name = 'GrossProfit'") > 0;

        if (!purchaseGrossProfitExists)
        {
            _logger.LogInformation("PurchaseVouchersテーブルにGrossProfitカラムを追加します...");
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[PurchaseVouchers]
                ADD [GrossProfit] DECIMAL(16,4) NULL");
        }

        // InventoryAdjustmentsテーブル
        var adjustmentGrossProfitExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') 
            AND name = 'GrossProfit'") > 0;

        if (!adjustmentGrossProfitExists)
        {
            _logger.LogInformation("InventoryAdjustmentsテーブルにGrossProfitカラムを追加します...");
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[InventoryAdjustments]
                ADD [GrossProfit] DECIMAL(16,4) NULL");
        }
    }

    /// <summary>
    /// VoucherIdカラムのサイズ更新
    /// </summary>
    private async Task UpdateVoucherIdSizeAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        // 現在のVoucherIdのサイズを確認
        var currentSize = await connection.ExecuteScalarAsync<int>(@"
            SELECT max_length 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
            AND name = 'VoucherId'");

        // NVARCHAR(50) = 100バイト、NVARCHAR(100) = 200バイト
        if (currentSize == 100) // 現在50文字
        {
            // データが存在するか確認
            var hasData = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM SalesVouchers") > 0;

            if (!hasData)
            {
                _logger.LogInformation("VoucherIdカラムのサイズを100文字に拡張します...");
                
                // 主キー制約を一時的に削除
                await connection.ExecuteAsync(@"
                    ALTER TABLE [dbo].[SalesVouchers] 
                    DROP CONSTRAINT PK_SalesVouchers");

                // カラムサイズを変更
                await connection.ExecuteAsync(@"
                    ALTER TABLE [dbo].[SalesVouchers] 
                    ALTER COLUMN VoucherId NVARCHAR(100) NOT NULL");

                // 主キー制約を再作成
                await connection.ExecuteAsync(@"
                    ALTER TABLE [dbo].[SalesVouchers] 
                    ADD CONSTRAINT PK_SalesVouchers PRIMARY KEY (VoucherId, LineNumber)");

                _logger.LogInformation("VoucherIdカラムのサイズを拡張しました。");
            }
            else
            {
                _logger.LogWarning("SalesVouchersテーブルにデータが存在するため、VoucherIdのサイズ変更をスキップします。");
            }
        }
    }

    /// <summary>
    /// ProductNameカラムの追加
    /// </summary>
    private async Task AddProductNameColumnAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        var productNameExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
            AND name = 'ProductName'") > 0;

        if (!productNameExists)
        {
            _logger.LogInformation("SalesVouchersテーブルにProductNameカラムを追加します...");
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[SalesVouchers]
                ADD [ProductName] NVARCHAR(100) NULL");
            _logger.LogInformation("ProductNameカラムを追加しました。");
        }
    }
    
    /// <summary>
    /// 月計カラムの追加
    /// </summary>
    private async Task AddMonthlyColumnsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        // 追加する月計カラムのリスト
        var monthlyColumns = new[]
        {
            // 月計売上関連
            ("MonthlySalesQuantity", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlySalesAmount", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlySalesReturnQuantity", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlySalesReturnAmount", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            
            // 月計仕入関連
            ("MonthlyPurchaseQuantity", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlyPurchaseAmount", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlyPurchaseReturnQuantity", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlyPurchaseReturnAmount", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            
            // 月計在庫調整関連
            ("MonthlyInventoryAdjustmentQuantity", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlyInventoryAdjustmentAmount", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            
            // 月計加工・振替関連
            ("MonthlyProcessingQuantity", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlyProcessingAmount", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlyTransferQuantity", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlyTransferAmount", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            
            // 月計粗利益関連
            ("MonthlyGrossProfit", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlyWalkingAmount", "DECIMAL(18,4) NOT NULL DEFAULT 0"),
            ("MonthlyIncentiveAmount", "DECIMAL(18,4) NOT NULL DEFAULT 0")
        };
        
        foreach (var (columnName, columnDefinition) in monthlyColumns)
        {
            var columnExists = await connection.ExecuteScalarAsync<int>($@"
                SELECT COUNT(*) 
                FROM sys.columns 
                WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') 
                AND name = '{columnName}'") > 0;

            if (!columnExists)
            {
                _logger.LogInformation($"CpInventoryMasterテーブルに{columnName}カラムを追加します...");
                await connection.ExecuteAsync($@"
                    ALTER TABLE [dbo].[CpInventoryMaster]
                    ADD [{columnName}] {columnDefinition}");
            }
        }
    }
    
    /// <summary>
    /// 必要なストアドプロシージャの作成
    /// </summary>
    private async Task CreateStoredProceduresAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        // ストアドプロシージャが存在するかチェック
        var procedureExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.objects 
            WHERE type = 'P' AND name = 'sp_CreateCpInventoryFromInventoryMasterWithProductInfo'") > 0;

        if (!procedureExists)
        {
            _logger.LogInformation("CP在庫マスタ作成用ストアドプロシージャを作成します...");
            
            var procedureSql = @"
CREATE PROCEDURE sp_CreateCpInventoryFromInventoryMasterWithProductInfo
    @DataSetId NVARCHAR(50),
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        INSERT INTO CpInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            ProductName, ProductCategory1, ProductCategory2, Unit, StandardPrice,
            JobDate, DataSetId,
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice, DailyFlag,
            DailySalesQuantity, DailySalesAmount, DailySalesReturnQuantity, DailySalesReturnAmount,
            DailyPurchaseQuantity, DailyPurchaseAmount, DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount,
            DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount,
            DailyProcessingQuantity, DailyProcessingAmount,
            DailyTransferQuantity, DailyTransferAmount,
            DailyReceiptQuantity, DailyReceiptAmount,
            DailyShipmentQuantity, DailyShipmentAmount,
            DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, DailyDiscountAmount,
            DailyStock, DailyStockAmount, DailyUnitPrice,
            CreatedDate, UpdatedDate
        )
        SELECT 
            im.ProductCode, im.GradeCode, im.ClassCode, im.ShippingMarkCode, im.ShippingMarkName,
            im.ProductName, 
            CASE 
                WHEN LEFT(im.ShippingMarkName, 4) = '9aaa' THEN '8'
                WHEN LEFT(im.ShippingMarkName, 4) = '1aaa' THEN '6'
                WHEN LEFT(im.ShippingMarkName, 4) = '0999' THEN '6'
                ELSE ISNULL(pm.ProductCategory1, '00')
            END AS ProductCategory1,
            ISNULL(pm.ProductCategory2, '00') AS ProductCategory2,
            im.Unit, im.StandardPrice, im.JobDate, @DataSetId,
            im.CurrentStock AS PreviousDayStock, 
            im.CurrentStockAmount AS PreviousDayStockAmount, 
            CASE WHEN im.CurrentStock = 0 THEN 0 ELSE im.CurrentStockAmount / im.CurrentStock END AS PreviousDayUnitPrice,
            '9' AS DailyFlag,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            GETDATE(), GETDATE()
        FROM InventoryMaster im
        LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode
        WHERE im.JobDate = @JobDate;
        
        SELECT @@ROWCOUNT AS CreatedCount;
        
        COMMIT TRANSACTION;
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END";
            
            await connection.ExecuteAsync(procedureSql);
            _logger.LogInformation("CP在庫マスタ作成用ストアドプロシージャを作成しました。");
        }
    }
}