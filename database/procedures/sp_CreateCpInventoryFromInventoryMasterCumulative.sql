-- =============================================
-- 累積管理対応版 CP在庫マスタ作成ストアドプロシージャ
-- 作成日: 2025-07-10
-- 修正日: 2025-07-30 - DataSetId削除（仮テーブル設計）
-- 説明: 在庫マスタから指定日以前のアクティブな在庫で、対象期間の伝票に関連する5項目キーのレコードのみをCP在庫マスタにコピー
-- =============================================
USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative')
BEGIN
    DROP PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative;
END
GO

CREATE PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative
    @JobDate DATE = NULL  -- NULLの場合は全期間対象（DataSetIdパラメータは削除）
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CreatedCount INT = 0;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- CP在庫マスタは仮テーブルなので全データを削除
        TRUNCATE TABLE CpInventoryMaster;
        
        -- 在庫マスタから伝票に関連する商品をCP在庫マスタに挿入
        INSERT INTO CpInventoryMaster (
            -- 5項目キー
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            ManualShippingMark,  -- 手入力荷印（追加）
            -- 管理項目
            ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
            JobDate, CreatedDate, UpdatedDate,
            -- 前日在庫
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice,
            -- 当日在庫
            DailyStock, DailyStockAmount, DailyUnitPrice,
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
            GrossProfitOnSales,
            PurchaseDiscountAmount,
            InventoryDiscountAmount,
            CalculatedDailyStock,
            StockDifference,
            StockDifferenceRatio,
            IsDifferenceSignificant,
            FinalPurchaseDate,
            ProductManagerCode,
            DepartmentCode
        )
        SELECT 
            -- 5項目キー
            im.ProductCode, im.GradeCode, im.ClassCode, 
            im.ShippingMarkCode, 
            ISNULL(sm.ShippingMarkName, '荷' + im.ShippingMarkCode) as ShippingMarkName,  -- 荷印マスタ名
            im.ManualShippingMark as ManualShippingMark,  -- InventoryMasterの手入力値
            -- 管理項目
            im.ProductName, 
            COALESCE(u.UnitName, im.Unit) AS Unit,
            im.StandardPrice, 
            im.ProductCategory1, im.ProductCategory2,
            im.JobDate, 
            GETDATE() AS CreatedDate, 
            GETDATE() AS UpdatedDate,
            -- 前日在庫（在庫マスタの現在庫をコピー）
            im.CurrentStock AS PreviousDayStock,
            im.CurrentStock * im.UnitPrice AS PreviousDayStockAmount,
            im.UnitPrice AS PreviousDayUnitPrice,
            -- 当日在庫（初期値は前日在庫と同じ）
            im.CurrentStock AS DailyStock,
            im.CurrentStock * im.UnitPrice AS DailyStockAmount,
            im.UnitPrice AS DailyUnitPrice,
            '9' AS DailyFlag,  -- 未処理
            -- 日計22個（すべて0で初期化）
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            -- 月計17個（すべて0で初期化）
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            -- その他
            0 AS GrossProfitOnSales,
            0 AS PurchaseDiscountAmount,
            0 AS InventoryDiscountAmount,
            0 AS CalculatedDailyStock,
            0 AS StockDifference,
            0 AS StockDifferenceRatio,
            0 AS IsDifferenceSignificant,
            im.FinalPurchaseDate,
            im.ProductManagerCode,
            'DeptA' AS DepartmentCode  -- 固定値（DataSetId廃止のため）
        FROM InventoryMaster im
        LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode
        LEFT JOIN UnitMaster u ON pm.UnitCode = u.UnitCode
        LEFT JOIN ShippingMarkMaster sm ON im.ShippingMarkCode = sm.ShippingMarkCode
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
            WHERE (@JobDate IS NULL OR sv.JobDate <= @JobDate) 
            AND sv.ProductCode = im.ProductCode
            AND sv.GradeCode = im.GradeCode
            AND sv.ClassCode = im.ClassCode
            AND sv.ShippingMarkCode = im.ShippingMarkCode
            AND sv.ShippingMarkName = im.ManualShippingMark
            UNION
            SELECT 1 FROM PurchaseVouchers pv
            WHERE (@JobDate IS NULL OR pv.JobDate <= @JobDate)
            AND pv.ProductCode = im.ProductCode
            AND pv.GradeCode = im.GradeCode
            AND pv.ClassCode = im.ClassCode
            AND pv.ShippingMarkCode = im.ShippingMarkCode
            AND pv.ShippingMarkName = im.ManualShippingMark
            UNION
            SELECT 1 FROM InventoryAdjustments ia
            WHERE (@JobDate IS NULL OR ia.JobDate <= @JobDate)
            AND ia.ProductCode = im.ProductCode
            AND ia.GradeCode = im.GradeCode
            AND ia.ClassCode = im.ClassCode
            AND ia.ShippingMarkCode = im.ShippingMarkCode
            AND ia.ShippingMarkName = im.ManualShippingMark
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

PRINT '✓ sp_CreateCpInventoryFromInventoryMasterCumulative を作成/更新しました（DataSetId削除版）';
GO