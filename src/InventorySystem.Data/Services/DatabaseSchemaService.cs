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
            await AddDepartmentCodeColumnAsync();
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
    /// DepartmentCodeカラムの追加
    /// </summary>
    private async Task AddDepartmentCodeColumnAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        var departmentCodeExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') 
            AND name = 'DepartmentCode'") > 0;

        if (!departmentCodeExists)
        {
            _logger.LogInformation("CpInventoryMasterテーブルにDepartmentCodeカラムを追加します...");
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[CpInventoryMaster]
                ADD [DepartmentCode] NVARCHAR(10) NOT NULL DEFAULT 'DeptA'");
            _logger.LogInformation("DepartmentCodeカラムを追加しました。");
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
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
            ProductName, ProductCategory1, ProductCategory2, Unit, StandardPrice,
            JobDate, DataSetId, DepartmentCode,
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice, DailyFlag,
            -- 日計売上関連
            DailySalesQuantity, DailySalesAmount, DailySalesReturnQuantity, DailySalesReturnAmount,
            -- 日計仕入関連
            DailyPurchaseQuantity, DailyPurchaseAmount, DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount,
            -- 日計在庫調整関連
            DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount,
            -- 日計加工・振替関連
            DailyProcessingQuantity, DailyProcessingAmount,
            DailyTransferQuantity, DailyTransferAmount,
            -- 日計出入荷関連
            DailyReceiptQuantity, DailyReceiptAmount,
            DailyShipmentQuantity, DailyShipmentAmount,
            -- 日計粗利関連
            DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, DailyDiscountAmount,
            -- 日計在庫関連
            DailyStock, DailyStockAmount, DailyUnitPrice,
            -- 月計売上関連
            MonthlySalesQuantity, MonthlySalesAmount, MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            -- 月計仕入関連
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount, MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            -- 月計在庫調整関連
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            -- 月計加工・振替関連
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            MonthlyTransferQuantity, MonthlyTransferAmount,
            -- 月計粗利関連
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount,
            -- 作成日時
            CreatedDate, UpdatedDate
        )
        SELECT 
            im.ProductCode, im.GradeCode, im.ClassCode, im.ShippingMarkCode, im.ManualShippingMark,
            im.ProductName, 
            CASE 
                WHEN LEFT(im.ManualShippingMark, 4) = '9aaa' THEN '8'
                WHEN LEFT(im.ManualShippingMark, 4) = '1aaa' THEN '6'
                WHEN LEFT(im.ManualShippingMark, 4) = '0999' THEN '6'
                ELSE ISNULL(pm.ProductCategory1, '00')
            END AS ProductCategory1,
            ISNULL(pm.ProductCategory2, '00') AS ProductCategory2,
            im.Unit, im.StandardPrice, im.JobDate, @DataSetId, 'DeptA' AS DepartmentCode,
            im.CurrentStock AS PreviousDayStock, 
            im.CurrentStockAmount AS PreviousDayStockAmount, 
            CASE WHEN im.CurrentStock = 0 THEN 0 ELSE im.CurrentStockAmount / im.CurrentStock END AS PreviousDayUnitPrice,
            '9' AS DailyFlag,
            -- 日計売上関連（4項目）
            0, 0, 0, 0,
            -- 日計仕入関連（4項目）
            0, 0, 0, 0,
            -- 日計在庫調整関連（2項目）
            0, 0,
            -- 日計加工・振替関連（4項目）
            0, 0, 0, 0,
            -- 日計出入荷関連（4項目）
            0, 0, 0, 0,
            -- 日計粗利関連（4項目）
            0, 0, 0, 0,
            -- 日計在庫関連（3項目）
            0, 0, 0,
            -- 月計売上関連（4項目）
            0, 0, 0, 0,
            -- 月計仕入関連（4項目）
            0, 0, 0, 0,
            -- 月計在庫調整関連（2項目）
            0, 0,
            -- 月計加工・振替関連（4項目）
            0, 0, 0, 0,
            -- 月計粗利関連（3項目）
            0, 0, 0,
            -- 作成日時（2項目）
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

    /// <summary>
    /// CP在庫マスタ作成用ストアドプロシージャV2の作成
    /// </summary>
    public async Task CreateCpInventoryStoredProcedureV2Async()
    {
        using var connection = new SqlConnection(_connectionString);
        
        _logger.LogInformation("CP在庫マスタ作成用ストアドプロシージャV2を作成します...");
        
        // 既存のストアドプロシージャを削除
        await connection.ExecuteAsync(@"
            IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_CreateCpInventoryFromInventoryMasterWithProductInfoV2')
            DROP PROCEDURE sp_CreateCpInventoryFromInventoryMasterWithProductInfoV2");

        var procedureSql = @"
CREATE PROCEDURE sp_CreateCpInventoryFromInventoryMasterWithProductInfoV2
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CreatedCount INT = 0;
    DECLARE @InitialInventoryDate DATE;
    DECLARE @ErrorMessage NVARCHAR(MAX);
    
    PRINT 'CP在庫マスタ作成処理V2を開始します（仮テーブル設計）...';
    PRINT 'ジョブ日付: ' + CONVERT(VARCHAR(10), @JobDate, 120);
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- 初期在庫基準日の決定（前日または直近の在庫データ日付）
        SELECT TOP 1 @InitialInventoryDate = JobDate
        FROM InventoryMaster
        WHERE JobDate <= @JobDate
        ORDER BY JobDate DESC;
        
        IF @InitialInventoryDate IS NULL
        BEGIN
            SET @ErrorMessage = '初期在庫データが見つかりません。ジョブ日付: ' + CONVERT(VARCHAR(10), @JobDate, 120);
            PRINT 'エラー: ' + @ErrorMessage;
            RAISERROR(@ErrorMessage, 16, 1);
        END
        
        PRINT '初期在庫基準日: ' + CONVERT(VARCHAR(10), @InitialInventoryDate, 120);
        
        -- 仮テーブル設計：全データを削除してから再作成
        TRUNCATE TABLE CpInventoryMaster;
        
        -- CP在庫マスタの作成
        INSERT INTO CpInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
            ProductName, ProductCategory1, ProductCategory2, Unit, StandardPrice,
            JobDate, DepartmentCode,
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice, DailyFlag,
            -- 日計売上関連
            DailySalesQuantity, DailySalesAmount, DailySalesReturnQuantity, DailySalesReturnAmount,
            -- 日計仕入関連
            DailyPurchaseQuantity, DailyPurchaseAmount, DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount,
            -- 日計在庫調整関連
            DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount,
            -- 日計加工・振替関連
            DailyProcessingQuantity, DailyProcessingAmount,
            DailyTransferQuantity, DailyTransferAmount,
            -- 日計出入荷関連
            DailyReceiptQuantity, DailyReceiptAmount,
            DailyShipmentQuantity, DailyShipmentAmount,
            -- 日計粗利関連
            DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, DailyDiscountAmount,
            -- 日計在庫関連
            DailyStock, DailyStockAmount, DailyUnitPrice,
            -- 月計売上関連
            MonthlySalesQuantity, MonthlySalesAmount, MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            -- 月計仕入関連
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount, MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            -- 月計在庫調整関連
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            -- 月計加工・振替関連
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            MonthlyTransferQuantity, MonthlyTransferAmount,
            -- 月計粗利関連
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount,
            -- 作成日時
            CreatedDate, UpdatedDate
        )
        SELECT 
            im.ProductCode, im.GradeCode, im.ClassCode, im.ShippingMarkCode, im.ManualShippingMark,
            ISNULL(pm.ProductName, im.ProductName) AS ProductName,
            CASE 
                WHEN LEFT(im.ManualShippingMark, 4) = '9aaa' THEN '8'
                WHEN LEFT(im.ManualShippingMark, 4) = '1aaa' THEN '6'
                WHEN LEFT(im.ManualShippingMark, 4) = '0999' THEN '6'
                ELSE ISNULL(pm.ProductCategory1, '00')
            END AS ProductCategory1,
            ISNULL(pm.ProductCategory2, '00') AS ProductCategory2,
            ISNULL(pm.UnitCode, im.Unit) AS Unit, 
            ISNULL(pm.StandardPrice, im.StandardPrice) AS StandardPrice,
            @JobDate AS JobDate, 'DeptA' AS DepartmentCode,
            im.CurrentStock AS PreviousDayStock, 
            im.CurrentStockAmount AS PreviousDayStockAmount, 
            CASE 
                WHEN im.CurrentStock = 0 THEN 0 
                ELSE ROUND(im.CurrentStockAmount / NULLIF(im.CurrentStock, 0), 4)
            END AS PreviousDayUnitPrice,
            '9' AS DailyFlag,
            -- 日計売上関連（4項目）
            0, 0, 0, 0,
            -- 日計仕入関連（4項目）
            0, 0, 0, 0,
            -- 日計在庫調整関連（2項目）
            0, 0,
            -- 日計加工・振替関連（4項目）
            0, 0, 0, 0,
            -- 日計出入荷関連（4項目）
            0, 0, 0, 0,
            -- 日計粗利関連（4項目）
            0, 0, 0, 0,
            -- 日計在庫関連（3項目）
            0, 0, 0,
            -- 月計売上関連（4項目）
            0, 0, 0, 0,
            -- 月計仕入関連（4項目）
            0, 0, 0, 0,
            -- 月計在庫調整関連（2項目）
            0, 0,
            -- 月計加工・振替関連（4項目）
            0, 0, 0, 0,
            -- 月計粗利関連（3項目）
            0, 0, 0,
            -- 作成日時（2項目）
            GETDATE(), GETDATE()
        FROM InventoryMaster im
        LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode
        WHERE im.JobDate = @InitialInventoryDate;
        
        SET @CreatedCount = @@ROWCOUNT;
        
        PRINT 'CP在庫マスタ作成完了: ' + CAST(@CreatedCount AS VARCHAR(10)) + '件';
        
        -- 0除算防止と異常値チェック
        IF @CreatedCount = 0
        BEGIN
            PRINT '警告: CP在庫マスタが1件も作成されませんでした';
        END
        ELSE
        BEGIN
            -- 異常値のチェック
            DECLARE @NegativeStockCount INT;
            SELECT @NegativeStockCount = COUNT(*)
            FROM CpInventoryMaster
            WHERE DataSetId = @DataSetId AND PreviousDayStock < 0;
            
            IF @NegativeStockCount > 0
            BEGIN
                PRINT '警告: マイナス在庫が ' + CAST(@NegativeStockCount AS VARCHAR(10)) + ' 件見つかりました';
            END
        END
        
        SELECT @CreatedCount AS CreatedCount, @InitialInventoryDate AS InitialInventoryDate;
        
        COMMIT TRANSACTION;
        PRINT 'トランザクションをコミットしました';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
            PRINT 'エラーが発生したためトランザクションをロールバックしました';
        END
        
        SET @ErrorMessage = 'CP在庫マスタ作成エラー: ' + ERROR_MESSAGE();
        PRINT @ErrorMessage;
        THROW;
    END CATCH
    
    PRINT 'CP在庫マスタ作成処理V2を完了しました';
END";
        
        await connection.ExecuteAsync(procedureSql);
        _logger.LogInformation("CP在庫マスタ作成用ストアドプロシージャV2を作成しました");
    }

    /// <summary>
    /// CP在庫マスタ作成テスト用メソッド（仮テーブル設計）
    /// </summary>
    public async Task<int> TestCpInventoryCreationAsync(DateTime jobDate)
    {
        using var connection = new SqlConnection(_connectionString);
        
        _logger.LogInformation("CP在庫マスタ作成テストを開始します（仮テーブル設計） - ジョブ日付: {JobDate}", 
            jobDate.ToString("yyyy-MM-dd"));
        
        // 作成前の件数を確認
        var beforeCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM CpInventoryMaster");
        
        _logger.LogInformation("作成前のCP在庫マスタ件数: {Count}件", beforeCount);
        
        // 在庫マスタの存在確認
        var inventoryMasterCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM InventoryMaster WHERE JobDate <= @JobDate",
            new { JobDate = jobDate });
        
        _logger.LogInformation("対象在庫マスタ件数: {Count}件", inventoryMasterCount);
        
        if (inventoryMasterCount == 0)
        {
            _logger.LogWarning("在庫マスタにデータが存在しません");
            return 0;
        }
        
        // ストアドプロシージャV2を実行（仮テーブル設計）
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
            "EXEC sp_CreateCpInventoryFromInventoryMasterWithProductInfoV2 @JobDate",
            new { JobDate = jobDate });
        
        var createdCount = result?.CreatedCount ?? 0;
        var initialInventoryDate = result?.InitialInventoryDate;
        
        _logger.LogInformation("CP在庫マスタ作成完了: {CreatedCount}件作成", (object)createdCount);
        _logger.LogInformation("初期在庫基準日: {InitialInventoryDate}", (object)(initialInventoryDate?.ToString("yyyy-MM-dd") ?? "null"));
        
        // 作成後の件数を確認（仮テーブル設計：全レコード）
        var afterCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM CpInventoryMaster");
        
        _logger.LogInformation("作成後のCP在庫マスタ件数: {Count}件", afterCount);
        
        return createdCount;
    }
}