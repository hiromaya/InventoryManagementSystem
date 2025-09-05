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
            ShippingMarkName,
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
            AveragePrice,
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
            -- 荷印マスタから表示名を取得（存在しない場合は空文字）
            ISNULL(sm.ShippingMarkName, '') AS ShippingMarkName,
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
            -- 前日在庫の設定を条件分岐（DailyFlag='9'は前月末在庫）
            CASE 
                WHEN im.DailyFlag = '9' THEN im.PreviousMonthQuantity
                ELSE im.CurrentStock
            END AS PreviousDayStock,
            CASE 
                WHEN im.DailyFlag = '9' THEN im.PreviousMonthAmount
                ELSE im.CurrentStockAmount
            END AS PreviousDayStockAmount,
            -- 前日在庫単価の計算（前月末在庫は単価を直接計算）
            CASE 
                WHEN im.DailyFlag = '9' AND im.PreviousMonthQuantity != 0 
                    THEN ROUND(im.PreviousMonthAmount / im.PreviousMonthQuantity, 4)
                WHEN im.DailyFlag = '9' AND im.PreviousMonthQuantity = 0 
                    THEN ISNULL(im.StandardPrice, 0)  -- StandardPriceをフォールバック使用
                ELSE COALESCE(NULLIF(im.StandardPrice, 0), im.AveragePrice, 0)
            END AS PreviousDayUnitPrice,
            -- 当日在庫（初期値は前日と同じ）
            CASE 
                WHEN im.DailyFlag = '9' THEN im.PreviousMonthQuantity
                ELSE im.CurrentStock
            END AS DailyStock,
            CASE 
                WHEN im.DailyFlag = '9' THEN im.PreviousMonthAmount
                ELSE im.CurrentStockAmount
            END AS DailyStockAmount,

            -- 当日在庫単価
            CASE 
                WHEN im.DailyFlag = '9' AND im.PreviousMonthQuantity != 0 
                    THEN ROUND(im.PreviousMonthAmount / im.PreviousMonthQuantity, 4)
                WHEN im.DailyFlag = '9' AND im.PreviousMonthQuantity = 0 
                    THEN ISNULL(im.StandardPrice, 0)
                ELSE COALESCE(NULLIF(im.StandardPrice, 0), im.AveragePrice, 0)
            END AS DailyUnitPrice,

            -- AveragePrice（同じロジック）
            CASE 
                WHEN im.DailyFlag = '9' AND im.PreviousMonthQuantity != 0 
                    THEN ROUND(im.PreviousMonthAmount / im.PreviousMonthQuantity, 4)
                WHEN im.DailyFlag = '9' AND im.PreviousMonthQuantity = 0 
                    THEN ISNULL(im.StandardPrice, 0)
                ELSE COALESCE(NULLIF(im.StandardPrice, 0), im.AveragePrice, 0)
            END AS AveragePrice,

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
        LEFT JOIN ShippingMarkMaster sm ON im.ShippingMarkCode = sm.ShippingMarkCode
        WHERE im.IsActive = 1  -- アクティブな在庫のみ
        AND im.JobDate = @JobDate  -- 指定日のレコードのみ
        
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