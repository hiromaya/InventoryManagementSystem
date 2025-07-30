-- =============================================
-- 累積管理対応版 CP在庫マスタ作成ストアドプロシージャ（全期間対応版）
-- 作成日: 2025-07-10
-- 説明: 在庫マスタから全期間の伝票に関連する5項目キーのレコードをCP在庫マスタにコピー
-- =============================================
USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative')
BEGIN
    DROP PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative;
END
GO

CREATE PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative
    @JobDate DATE = NULL  -- NULLの場合は全期間対象
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CreatedCount INT = 0;
    DECLARE @ErrorMessage NVARCHAR(4000);
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- 既存のCP在庫マスタを全削除（仮テーブル設計）
        TRUNCATE TABLE CpInventoryMaster;
        
        -- 在庫マスタから伝票に関連する商品をCP在庫マスタに挿入
        INSERT INTO CpInventoryMaster (
            -- 5項目キー
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            -- 管理項目
            ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
            JobDate, CreatedDate, UpdatedDate,
            -- 前日在庫（在庫マスタのCurrentStockをコピー）
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice,
            -- 当日在庫（初期値は前日在庫と同じ）
            DailyStock, DailyStockAmount, DailyUnitPrice,
            DailyFlag,
            -- 日計22個（すべて0で初期化）
            DailySalesQuantity, DailySalesAmount, 
            DailySalesReturnQuantity, DailySalesReturnAmount,
            DailyPurchaseQuantity, DailyPurchaseAmount,
            DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount,
            DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount,
            DailyProcessingQuantity, DailyProcessingAmount,
            DailyTransferQuantity, DailyTransferAmount,
            DailyReceiptQuantity, DailyReceiptAmount,
            DailyShipmentQuantity, DailyShipmentAmount,
            DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, DailyDiscountAmount,
            -- 月計17個（すべて0で初期化）
            MonthlySalesQuantity, MonthlySalesAmount,
            MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount,
            MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            MonthlyTransferQuantity, MonthlyTransferAmount,
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount,
            -- 部門コード
            DepartmentCode
        )
        SELECT 
            -- 5項目キー
            im.ProductCode, im.GradeCode, im.ClassCode, im.ShippingMarkCode, im.ShippingMarkName,
            -- 管理項目
            ISNULL(pm.ProductName, ''),
            ISNULL(u.UnitName, ''),  -- UnitMasterから単位名を取得
            ISNULL(pm.StandardPrice, 0),
            ISNULL(pm.ProductCategory1, ''),
            ISNULL(pm.ProductCategory2, ''),
            ISNULL(@JobDate, GETDATE()), GETDATE(), GETDATE(),
            -- 前日在庫として現在在庫を使用
            im.CurrentStock, im.CurrentStockAmount,
            CASE WHEN im.CurrentStock > 0 THEN im.CurrentStockAmount / im.CurrentStock ELSE 0 END,
            -- 当日在庫（初期値として前日在庫と同じ）
            im.CurrentStock, im.CurrentStockAmount,
            CASE WHEN im.CurrentStock > 0 THEN im.CurrentStockAmount / im.CurrentStock ELSE 0 END,
            '9',
            -- 日計22個の0
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            -- 月計17個の0
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            -- 部門コード（固定値：DataSetId管理廃止）
            'DeptA'
        FROM InventoryMaster im
        LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode
        LEFT JOIN UnitMaster u ON pm.UnitCode = u.UnitCode  -- 正しい結合
        WHERE EXISTS (
            -- 伝票に存在する5項目キーのみ（@JobDateがNULLの場合は全期間）
            SELECT 1 FROM SalesVouchers sv 
            WHERE (@JobDate IS NULL OR sv.JobDate = @JobDate)
            AND sv.ProductCode = im.ProductCode
            AND sv.GradeCode = im.GradeCode
            AND sv.ClassCode = im.ClassCode
            AND sv.ShippingMarkCode = im.ShippingMarkCode
            AND sv.ShippingMarkName = im.ShippingMarkName
            UNION
            SELECT 1 FROM PurchaseVouchers pv
            WHERE (@JobDate IS NULL OR pv.JobDate = @JobDate)
            AND pv.ProductCode = im.ProductCode
            AND pv.GradeCode = im.GradeCode
            AND pv.ClassCode = im.ClassCode
            AND pv.ShippingMarkCode = im.ShippingMarkCode
            AND pv.ShippingMarkName = im.ShippingMarkName
            UNION
            SELECT 1 FROM InventoryAdjustments ia
            WHERE (@JobDate IS NULL OR ia.JobDate = @JobDate)
            AND ia.ProductCode = im.ProductCode
            AND ia.GradeCode = im.GradeCode
            AND ia.ClassCode = im.ClassCode
            AND ia.ShippingMarkCode = im.ShippingMarkCode
            AND ia.ShippingMarkName = im.ShippingMarkName
        );
        
        SET @CreatedCount = @@ROWCOUNT;
        
        COMMIT TRANSACTION;
        
        SELECT @CreatedCount as CreatedCount;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO