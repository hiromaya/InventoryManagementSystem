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
            -- その他（CpInventoryMasterに存在する場合）
            DepartmentCode
        )
        SELECT 
            -- 5項目キー
            im.ProductCode, 
            im.GradeCode, 
            im.ClassCode, 
            im.ShippingMarkCode, 
            im.ManualShippingMark,  -- ShippingMarkNameから変更済み
            -- 管理項目
            im.ProductName, 
            COALESCE(u.UnitName, im.Unit, '') AS Unit,
            im.StandardPrice, 
            im.ProductCategory1, 
            im.ProductCategory2,
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
        -- 伝票に存在する5項目キーのみ（伝票テーブルが存在する場合）
        AND EXISTS (
            SELECT 1 FROM SalesSlips ss
            WHERE (@JobDate IS NULL OR ss.SlipDate <= @JobDate) 
            AND ss.ProductCode = im.ProductCode
            AND ss.GradeCode = im.GradeCode
            AND ss.ClassCode = im.ClassCode
            AND ss.ShippingMarkCode = im.ShippingMarkCode
            AND ss.ManualShippingMark = im.ManualShippingMark
            UNION
            SELECT 1 FROM PurchaseSlips ps
            WHERE (@JobDate IS NULL OR ps.SlipDate <= @JobDate)
            AND ps.ProductCode = im.ProductCode
            AND ps.GradeCode = im.GradeCode
            AND ps.ClassCode = im.ClassCode
            AND ps.ShippingMarkCode = im.ShippingMarkCode
            AND ps.ManualShippingMark = im.ManualShippingMark
            UNION
            SELECT 1 FROM OrderSlips os
            WHERE (@JobDate IS NULL OR os.SlipDate <= @JobDate)
            AND os.ProductCode = im.ProductCode
            AND os.GradeCode = im.GradeCode
            AND os.ClassCode = im.ClassCode
            AND os.ShippingMarkCode = im.ShippingMarkCode
            AND os.ManualShippingMark = im.ManualShippingMark
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