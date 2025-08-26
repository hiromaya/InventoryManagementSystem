-- ====================================================================
-- CP在庫マスタ作成ストアドプロシージャ（ProductMasterなし版）
-- 作成日: 2025-01-07
-- 修正日: 2025-08-26（カラム数一致エラー修正）
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
        -- ※カラム数を正確に一致させる
        INSERT INTO dbo.CpInventoryMaster (
            -- 基本情報（5項目キー）: 5カラム
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
            -- 商品情報: 3カラム
            ProductName, Unit, StandardPrice,
            -- 商品分類: 2カラム
            ProductCategory1, ProductCategory2,
            -- マスタ名称: 2カラム
            GradeName, ClassName,
            -- 日付情報: 3カラム
            JobDate, CreatedDate, UpdatedDate,
            -- 前日在庫: 3カラム
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice,
            -- 当日在庫: 4カラム
            DailyStock, DailyStockAmount, DailyUnitPrice, DailyFlag,
            -- 当日売上: 4カラム
            DailySalesQuantity, DailySalesAmount,
            DailySalesReturnQuantity, DailySalesReturnAmount,
            -- 当日仕入: 4カラム
            DailyPurchaseQuantity, DailyPurchaseAmount,
            DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount,
            -- 当日調整: 2カラム
            DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount,
            -- 当日加工: 2カラム
            DailyProcessingQuantity, DailyProcessingAmount,
            -- 当日振替: 2カラム
            DailyTransferQuantity, DailyTransferAmount,
            -- 当日入出庫: 4カラム
            DailyReceiptQuantity, DailyReceiptAmount,
            DailyShipmentQuantity, DailyShipmentAmount,
            -- 当日粗利等: 5カラム
            DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, 
            DailyDiscountAmount, DailyPurchaseDiscountAmount,
            -- 月間売上: 4カラム
            MonthlySalesQuantity, MonthlySalesAmount,
            MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            -- 月間仕入: 4カラム
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount,
            MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            -- 月間調整: 2カラム
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            -- 月間加工: 2カラム
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            -- 月間振替: 2カラム
            MonthlyTransferQuantity, MonthlyTransferAmount,
            -- 月間粗利等: 3カラム
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount
            -- 合計: 63カラム
        )
        SELECT 
            -- 基本情報（5項目キー）: 5値
            im.ProductCode, im.GradeCode, im.ClassCode, im.ShippingMarkCode, im.ManualShippingMark,
            -- 商品情報: 3値
            im.ProductName, im.Unit, im.StandardPrice,
            -- 商品分類: 2値
            CASE 
                WHEN LEFT(im.ManualShippingMark, 4) = '9aaa' THEN '8'
                WHEN LEFT(im.ManualShippingMark, 4) = '1aaa' THEN '6'
                WHEN LEFT(im.ManualShippingMark, 4) = '0999' THEN '6'
                ELSE '00'
            END AS ProductCategory1,
            '00' AS ProductCategory2,
            -- マスタ名称: 2値
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
            -- 日付情報: 3値
            im.JobDate, GETDATE(), GETDATE(),
            -- 前日在庫: 3値（在庫数、金額、単価）
            im.CurrentStock, 
            im.CurrentStockAmount,
            CASE WHEN im.CurrentStock = 0 THEN 0 ELSE im.CurrentStockAmount / im.CurrentStock END,
            -- 当日在庫: 4値（在庫数、金額、単価、フラグ）
            0, 0, 0, '9',
            -- 当日売上: 4値
            0, 0, 0, 0,
            -- 当日仕入: 4値
            0, 0, 0, 0,
            -- 当日調整: 2値
            0, 0,
            -- 当日加工: 2値
            0, 0,
            -- 当日振替: 2値
            0, 0,
            -- 当日入出庫: 4値
            0, 0, 0, 0,
            -- 当日粗利等: 5値
            0, 0, 0, 0, 0,
            -- 月間売上: 4値
            0, 0, 0, 0,
            -- 月間仕入: 4値
            0, 0, 0, 0,
            -- 月間調整: 2値
            0, 0,
            -- 月間加工: 2値
            0, 0,
            -- 月間振替: 2値
            0, 0,
            -- 月間粗利等: 3値
            0, 0, 0
            -- 合計: 63値
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