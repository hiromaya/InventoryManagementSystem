-- ====================================================================
-- CP在庫マスタ作成ストアドプロシージャ（完全版・63カラム対応）
-- 作成日: 2025-08-26
-- 全63カラムに対応（DepartmentCode、GradeName、ClassName含む）
-- ====================================================================

CREATE OR ALTER PROCEDURE sp_CreateCpInventoryFromInventoryMasterWithProductInfo
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- CP在庫マスタに在庫マスタのデータを挿入（全63カラム）
        INSERT INTO dbo.CpInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
            ProductName, Unit, StandardPrice,
            ProductCategory1, ProductCategory2,
            JobDate,
            CreatedDate, UpdatedDate,
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice,
            DailyStock, DailyStockAmount, DailyUnitPrice,
            DailyFlag,
            DailySalesQuantity, DailySalesAmount,
            DailySalesReturnQuantity, DailySalesReturnAmount,
            DailyPurchaseQuantity, DailyPurchaseAmount,
            DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount,
            DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount,
            DailyProcessingQuantity, DailyProcessingAmount,
            DailyTransferQuantity, DailyTransferAmount,
            DailyReceiptQuantity, DailyReceiptAmount,
            DailyShipmentQuantity, DailyShipmentAmount,
            DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, DailyDiscountAmount, DailyPurchaseDiscountAmount,
            MonthlySalesQuantity, MonthlySalesAmount,
            MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount,
            MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            MonthlyTransferQuantity, MonthlyTransferAmount,
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount,
            DepartmentCode,
            GradeName, ClassName
        )
        SELECT 
            im.ProductCode, 
            im.GradeCode, 
            im.ClassCode, 
            im.ShippingMarkCode, 
            im.ManualShippingMark,
            im.ProductName, 
            im.Unit, 
            im.StandardPrice,
            -- 特殊処理ルール: 荷印名による商品分類1の変更
            CASE 
                WHEN LEFT(im.ManualShippingMark, 4) = '9aaa' THEN '8'
                WHEN LEFT(im.ManualShippingMark, 4) = '1aaa' THEN '6'
                WHEN LEFT(im.ManualShippingMark, 4) = '0999' THEN '6'
                ELSE '00'
            END,
            '00',  -- ProductCategory2
            im.JobDate,
            GETDATE(),  -- CreatedDate
            GETDATE(),  -- UpdatedDate
            im.CurrentStock,  -- PreviousDayStock
            im.CurrentStockAmount,  -- PreviousDayStockAmount
            CASE 
                WHEN im.CurrentStock = 0 THEN 0 
                ELSE im.CurrentStockAmount / im.CurrentStock 
            END,  -- PreviousDayUnitPrice
            0, 0, 0,  -- DailyStock, DailyStockAmount, DailyUnitPrice
            '9',  -- DailyFlag
            0, 0,  -- DailySalesQuantity, DailySalesAmount
            0, 0,  -- DailySalesReturnQuantity, DailySalesReturnAmount
            0, 0,  -- DailyPurchaseQuantity, DailyPurchaseAmount
            0, 0,  -- DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount
            0, 0,  -- DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount
            0, 0,  -- DailyProcessingQuantity, DailyProcessingAmount
            0, 0,  -- DailyTransferQuantity, DailyTransferAmount
            0, 0,  -- DailyReceiptQuantity, DailyReceiptAmount
            0, 0,  -- DailyShipmentQuantity, DailyShipmentAmount
            0, 0, 0, 0, 0,  -- DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, DailyDiscountAmount, DailyPurchaseDiscountAmount
            0, 0,  -- MonthlySalesQuantity, MonthlySalesAmount
            0, 0,  -- MonthlySalesReturnQuantity, MonthlySalesReturnAmount
            0, 0,  -- MonthlyPurchaseQuantity, MonthlyPurchaseAmount
            0, 0,  -- MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount
            0, 0,  -- MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount
            0, 0,  -- MonthlyProcessingQuantity, MonthlyProcessingAmount
            0, 0,  -- MonthlyTransferQuantity, MonthlyTransferAmount
            0, 0, 0,  -- MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount
            'DeptA',  -- DepartmentCode
            ISNULL(gm.GradeName, 
                CASE 
                    WHEN im.GradeCode = '000' THEN '未分類'
                    WHEN im.GradeCode IS NULL OR im.GradeCode = '' THEN ''
                    ELSE 'Grade-' + im.GradeCode
                END),  -- GradeName
            ISNULL(cm.ClassName, 
                CASE 
                    WHEN im.ClassCode = '000' THEN '未分類'
                    WHEN im.ClassCode IS NULL OR im.ClassCode = '' THEN ''
                    ELSE 'Class-' + im.ClassCode
                END)  -- ClassName
        FROM dbo.InventoryMaster im
        LEFT JOIN dbo.GradeMaster gm ON im.GradeCode = gm.GradeCode
        LEFT JOIN dbo.ClassMaster cm ON im.ClassCode = cm.ClassCode
        WHERE im.JobDate = @JobDate
          AND im.IsActive = 1;
        
        -- 作成件数を返す
        SELECT @@ROWCOUNT AS CreatedCount;
        
        COMMIT TRANSACTION;
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO