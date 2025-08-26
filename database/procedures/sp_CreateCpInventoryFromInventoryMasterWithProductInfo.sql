-- ====================================================================
-- CP在庫マスタ作成ストアドプロシージャ（ProductMasterなし版）
-- 作成日: 2025-01-07
-- 修正日: 2025-07-19（ProductMasterが空のため簡易版）
-- 用途: 商品日報の大分類計表示のため、商品分類1を設定する
-- ====================================================================

CREATE OR ALTER PROCEDURE sp_CreateCpInventoryFromInventoryMasterWithProductInfo
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- CP在庫マスタに在庫マスタのデータを挿入（ProductMasterなし）
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
            DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, DailyDiscountAmount,
            MonthlySalesQuantity, MonthlySalesAmount,
            MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount,
            MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            MonthlyTransferQuantity, MonthlyTransferAmount,
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount,
            DepartmentCode
        )
        SELECT 
            im.ProductCode, im.GradeCode, im.ClassCode, im.ShippingMarkCode, im.ManualShippingMark,
            im.ProductName, im.Unit, im.StandardPrice,
            -- 特殊処理ルール: 荷印名による商品分類1の変更（ProductMasterなし）
            CASE 
                WHEN LEFT(im.ManualShippingMark, 4) = '9aaa' THEN '8'
                WHEN LEFT(im.ManualShippingMark, 4) = '1aaa' THEN '6'
                WHEN LEFT(im.ManualShippingMark, 4) = '0999' THEN '6'
                ELSE '00'  -- デフォルト値
            END AS ProductCategory1,
            '00' AS ProductCategory2,  -- デフォルト値
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
            GETDATE(), GETDATE(),
            im.CurrentStock, im.CurrentStockAmount,
            CASE WHEN im.CurrentStock = 0 THEN 0 ELSE im.CurrentStockAmount / im.CurrentStock END,
            0, 0, 0,
            '9',
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0,
            0, 0,
            0, 0,
            0, 0,
            0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0,
            0, 0,
            0, 0,
            0, 0, 0,
            'DeptA'
        FROM dbo.InventoryMaster im
        LEFT JOIN GradeMaster gm ON im.GradeCode = gm.GradeCode
        LEFT JOIN ClassMaster cm ON im.ClassCode = cm.ClassCode
        WHERE im.JobDate = @JobDate;
        
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