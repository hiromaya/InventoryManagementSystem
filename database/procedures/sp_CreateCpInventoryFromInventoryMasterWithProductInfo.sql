-- ====================================================================
-- CP在庫マスタ作成時に商品マスタから商品分類を取得し、特殊処理ルールを適用するストアドプロシージャ
-- 作成日: 2025-01-07
-- 用途: 商品日報の大分類計表示のため、商品分類1を正しく設定する
-- ====================================================================

CREATE OR ALTER PROCEDURE sp_CreateCpInventoryFromInventoryMasterWithProductInfo
    @DataSetId NVARCHAR(50),
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- CP在庫マスタに在庫マスタのデータを挿入（商品マスタと結合）
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
            MonthlySalesQuantity, MonthlySalesAmount, MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount, MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            MonthlyTransferQuantity, MonthlyTransferAmount,
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount,
            CreatedDate, UpdatedDate
        )
        SELECT 
            im.ProductCode, 
            im.GradeCode, 
            im.ClassCode, 
            im.ShippingMarkCode, 
            im.ShippingMarkName,
            im.ProductName, 
            -- 特殊処理ルール: 荷印名による商品分類1の変更
            CASE 
                WHEN LEFT(im.ShippingMarkName, 4) = '9aaa' THEN '8'
                WHEN LEFT(im.ShippingMarkName, 4) = '1aaa' THEN '6'
                WHEN LEFT(im.ShippingMarkName, 4) = '0999' THEN '6'
                ELSE ISNULL(pm.ProductCategory1, '00')
            END AS ProductCategory1,
            ISNULL(pm.ProductCategory2, '00') AS ProductCategory2,
            im.Unit, 
            im.StandardPrice,
            im.JobDate, 
            @DataSetId,
            im.CurrentStock AS PreviousDayStock, 
            im.CurrentStockAmount AS PreviousDayStockAmount, 
            CASE 
                WHEN im.CurrentStock = 0 THEN 0 
                ELSE im.CurrentStockAmount / im.CurrentStock 
            END AS PreviousDayUnitPrice,
            '9' AS DailyFlag,
            0, 0, 0, 0,  -- Sales
            0, 0, 0, 0,  -- Purchase
            0, 0,        -- Adjustment
            0, 0,        -- Processing
            0, 0,        -- Transfer
            0, 0,        -- Receipt/Shipment
            0, 0, 0, 0,  -- Profit/Walking/Incentive/Discount
            0, 0, 0,     -- Stock/StockAmount/UnitPrice
            0, 0, 0, 0,  -- Monthly Sales
            0, 0, 0, 0,  -- Monthly Purchase
            0, 0,        -- Monthly Adjustment
            0, 0,        -- Monthly Processing
            0, 0,        -- Monthly Transfer
            0, 0, 0,     -- Monthly Profit
            GETDATE(), 
            GETDATE()
        FROM InventoryMaster im
        LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode
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