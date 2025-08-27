USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative')
BEGIN
    DROP PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative;
END
GO

CREATE PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative
    @JobDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CreatedCount INT = 0;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- CP在庫マスタをクリア
        TRUNCATE TABLE CpInventoryMaster;
        
        -- 在庫マスタからCP在庫マスタへデータ移行
        INSERT INTO CpInventoryMaster (
            -- 5項目キー
            ProductCode, 
            GradeCode, 
            ClassCode, 
            ShippingMarkCode, 
            ManualShippingMark,
            -- 管理項目
            ProductName, 
            Unit, 
            StandardPrice, 
            ProductCategory1, 
            ProductCategory2,
            -- マスタ名称（追加）
            GradeName,
            ClassName,
            JobDate, 
            CreatedDate, 
            UpdatedDate,
            -- 前日在庫
            PreviousDayStock, 
            PreviousDayStockAmount, 
            PreviousDayUnitPrice,
            -- 当日在庫
            DailyStock, 
            DailyStockAmount, 
            DailyUnitPrice,
            DailyFlag,
            -- 日計項目（22個）
            DailySalesQuantity, DailySalesAmount, 
            DailySalesReturnQuantity, DailySalesReturnAmount,
            DailyPurchaseQuantity, DailyPurchaseAmount,
            DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount,
            DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount,
            DailyProcessingQuantity, DailyProcessingAmount,
            DailyTransferQuantity, DailyTransferAmount,
            DailyReceiptQuantity, DailyReceiptAmount,
            DailyShipmentQuantity, DailyShipmentAmount,
            DailyGrossProfit, DailyWalkingAmount, 
            DailyIncentiveAmount, DailyDiscountAmount,
            -- 月計項目（17個）
            MonthlySalesQuantity, MonthlySalesAmount,
            MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount,
            MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            MonthlyTransferQuantity, MonthlyTransferAmount,
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount,
            -- その他
            DepartmentCode
        )
        SELECT 
            -- 5項目キー
            im.ProductCode, 
            im.GradeCode, 
            im.ClassCode, 
            im.ShippingMarkCode, 
            im.ManualShippingMark,
            -- 管理項目
            ISNULL(pm.ProductName, im.ProductName) AS ProductName,
            COALESCE(u.UnitName, im.Unit, '') AS Unit,
            im.StandardPrice, 
            ISNULL(pm.ProductCategory1, im.ProductCategory1) AS ProductCategory1,
            ISNULL(pm.ProductCategory2, im.ProductCategory2) AS ProductCategory2,
            -- マスタ名称（マスタから取得）
            ISNULL(gm.GradeName, '') AS GradeName,
            ISNULL(cm.ClassName, '') AS ClassName,
            im.JobDate, 
            GETDATE() AS CreatedDate, 
            GETDATE() AS UpdatedDate,
            -- 前日在庫（InventoryMasterのCurrentStockとAveragePriceを使用）
            im.CurrentStock AS PreviousDayStock,
            im.CurrentStockAmount AS PreviousDayStockAmount,
            ISNULL(im.AveragePrice, 0) AS PreviousDayUnitPrice,
            -- 当日在庫（初期値は前日と同じ）
            im.CurrentStock AS DailyStock,
            im.CurrentStockAmount AS DailyStockAmount,
            ISNULL(im.AveragePrice, 0) AS DailyUnitPrice,
            ISNULL(im.DailyFlag, '9') AS DailyFlag,  -- 未処理フラグ
            -- 日計項目（22個すべて0で初期化）
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 
            ISNULL(im.DailyGrossProfit, 0),  -- DailyGrossProfitは既存値を使用
            0, 0, 0,
            -- 月計項目（17個すべて0で初期化）
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            -- その他
            ISNULL(im.DepartmentCode, 'DEFAULT') AS DepartmentCode
        FROM InventoryMaster im
        LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode
        LEFT JOIN UnitMaster u ON pm.UnitCode = u.UnitCode
        LEFT JOIN GradeMaster gm ON im.GradeCode = gm.GradeCode
        LEFT JOIN ClassMaster cm ON im.ClassCode = cm.ClassCode
        WHERE im.IsActive = 1  -- アクティブな在庫のみ
        AND (@JobDate IS NULL OR im.JobDate <= @JobDate)  -- 指定日以前
        -- 最新の在庫レコードのみ取得
        AND NOT EXISTS (
            SELECT 1 
            FROM InventoryMaster im2 
            WHERE im2.ProductCode = im.ProductCode
                AND im2.GradeCode = im.GradeCode
                AND im2.ClassCode = im.ClassCode
                AND im2.ShippingMarkCode = im.ShippingMarkCode
                AND im2.ManualShippingMark = im.ManualShippingMark
                AND im2.IsActive = 1
                AND (@JobDate IS NULL OR im2.JobDate <= @JobDate)
                AND im2.JobDate > im.JobDate
        )
        -- 伝票に存在する5項目キーのみ
        AND EXISTS (
            SELECT 1 FROM SalesVouchers sv
            WHERE (@JobDate IS NULL OR sv.VoucherDate <= @JobDate)  -- ← VoucherDateに修正
            AND sv.ProductCode = im.ProductCode
            AND sv.GradeCode = im.GradeCode
            AND sv.ClassCode = im.ClassCode
            AND sv.ShippingMarkCode = im.ShippingMarkCode
            AND sv.ManualShippingMark = im.ManualShippingMark
            UNION
            SELECT 1 FROM PurchaseVouchers pv
            WHERE (@JobDate IS NULL OR pv.VoucherDate <= @JobDate)  -- ← VoucherDateに修正
            AND pv.ProductCode = im.ProductCode
            AND pv.GradeCode = im.GradeCode
            AND pv.ClassCode = im.ClassCode
            AND pv.ShippingMarkCode = im.ShippingMarkCode
            AND pv.ManualShippingMark = im.ManualShippingMark
            UNION
            SELECT 1 FROM InventoryAdjustments ia
            WHERE (@JobDate IS NULL OR ia.JobDate <= @JobDate)  -- ← JobDateに修正
            AND ia.ProductCode = im.ProductCode
            AND ia.GradeCode = im.GradeCode
            AND ia.ClassCode = im.ClassCode
            AND ia.ShippingMarkCode = im.ShippingMarkCode
            AND ia.ManualShippingMark = im.ManualShippingMark
        );
        
        SET @CreatedCount = @@ROWCOUNT;
        
        COMMIT TRANSACTION;
        
        -- 作成件数を返す
        SELECT @CreatedCount AS CreatedCount;
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

PRINT '✓ sp_CreateCpInventoryFromInventoryMasterCumulative を作成しました';
GO