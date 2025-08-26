-- ====================================================================
-- CP在庫マスタ作成ストアドプロシージャ（完全修正版）
-- 作成日: 2025-01-07
-- 修正日: 2025-08-26
-- 修正内容：DepartmentCodeカラムを削除、カラム数を正確に一致
-- ====================================================================

CREATE OR ALTER PROCEDURE sp_CreateCpInventoryFromInventoryMasterWithProductInfo
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- CP在庫マスタに在庫マスタのデータを挿入
        INSERT INTO dbo.CpInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
            ProductName, Unit, StandardPrice,
            ProductCategory1, ProductCategory2,
            GradeName, ClassName,
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
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount
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
            END AS ProductCategory1,
            '00' AS ProductCategory2,
            ISNULL(gm.GradeName, 
                CASE 
                    WHEN im.GradeCode = '000' THEN '未分類'
                    WHEN im.GradeCode IS NULL OR im.GradeCode = '' THEN ''
                    ELSE 'Grade-' + im.GradeCode
                END) AS GradeName,
            ISNULL(cm.ClassName, 
                CASE 
                    WHEN im.ClassCode = '000' THEN '未分類'
                    WHEN im.ClassCode IS NULL OR im.ClassCode = '' THEN ''
                    ELSE 'Class-' + im.ClassCode
                END) AS ClassName,
            im.JobDate,
            GETDATE() AS CreatedDate,
            GETDATE() AS UpdatedDate,
            im.CurrentStock AS PreviousDayStock,
            im.CurrentStockAmount AS PreviousDayStockAmount,
            CASE 
                WHEN im.CurrentStock = 0 THEN 0 
                ELSE im.CurrentStockAmount / im.CurrentStock 
            END AS PreviousDayUnitPrice,
            0 AS DailyStock,
            0 AS DailyStockAmount,
            0 AS DailyUnitPrice,
            '9' AS DailyFlag,
            0 AS DailySalesQuantity,
            0 AS DailySalesAmount,
            0 AS DailySalesReturnQuantity,
            0 AS DailySalesReturnAmount,
            0 AS DailyPurchaseQuantity,
            0 AS DailyPurchaseAmount,
            0 AS DailyPurchaseReturnQuantity,
            0 AS DailyPurchaseReturnAmount,
            0 AS DailyInventoryAdjustmentQuantity,
            0 AS DailyInventoryAdjustmentAmount,
            0 AS DailyProcessingQuantity,
            0 AS DailyProcessingAmount,
            0 AS DailyTransferQuantity,
            0 AS DailyTransferAmount,
            0 AS DailyReceiptQuantity,
            0 AS DailyReceiptAmount,
            0 AS DailyShipmentQuantity,
            0 AS DailyShipmentAmount,
            0 AS DailyGrossProfit,
            0 AS DailyWalkingAmount,
            0 AS DailyIncentiveAmount,
            0 AS DailyDiscountAmount,
            0 AS DailyPurchaseDiscountAmount,
            0 AS MonthlySalesQuantity,
            0 AS MonthlySalesAmount,
            0 AS MonthlySalesReturnQuantity,
            0 AS MonthlySalesReturnAmount,
            0 AS MonthlyPurchaseQuantity,
            0 AS MonthlyPurchaseAmount,
            0 AS MonthlyPurchaseReturnQuantity,
            0 AS MonthlyPurchaseReturnAmount,
            0 AS MonthlyInventoryAdjustmentQuantity,
            0 AS MonthlyInventoryAdjustmentAmount,
            0 AS MonthlyProcessingQuantity,
            0 AS MonthlyProcessingAmount,
            0 AS MonthlyTransferQuantity,
            0 AS MonthlyTransferAmount,
            0 AS MonthlyGrossProfit,
            0 AS MonthlyWalkingAmount,
            0 AS MonthlyIncentiveAmount
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