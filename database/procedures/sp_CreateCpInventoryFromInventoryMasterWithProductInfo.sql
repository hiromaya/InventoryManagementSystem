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
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
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
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            ProductName, Unit, StandardPrice,
            -- 特殊処理ルール: 荷印名による商品分類1の変更（ProductMasterなし）
            CASE 
                WHEN LEFT(ShippingMarkName, 4) = '9aaa' THEN '8'
                WHEN LEFT(ShippingMarkName, 4) = '1aaa' THEN '6'
                WHEN LEFT(ShippingMarkName, 4) = '0999' THEN '6'
                ELSE '00'  -- デフォルト値
            END AS ProductCategory1,
            '00' AS ProductCategory2,  -- デフォルト値
            JobDate,
            GETDATE(), GETDATE(),
            CurrentStock, CurrentStockAmount,
            CASE WHEN CurrentStock = 0 THEN 0 ELSE CurrentStockAmount / CurrentStock END,
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
        FROM dbo.InventoryMaster
        WHERE JobDate = @JobDate;
        
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