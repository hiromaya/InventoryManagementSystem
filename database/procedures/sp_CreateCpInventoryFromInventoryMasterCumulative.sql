-- =============================================
-- 累積管理対応版 CP在庫マスタ作成ストアドプロシージャ
-- 作成日: 2025-07-10
-- 説明: 在庫マスタから当日の伝票に関連する5項目キーのレコードのみをCP在庫マスタにコピー
-- =============================================

USE InventoryManagementDB;
GO

-- 既存のストアドプロシージャが存在する場合は削除
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative')
BEGIN
    DROP PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative;
END
GO

CREATE PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative
    @DataSetId NVARCHAR(50),
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CreatedCount INT = 0;
    DECLARE @ErrorMessage NVARCHAR(4000);
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- 既存のCP在庫マスタを削除
        DELETE FROM CpInventoryMaster WHERE DataSetId = @DataSetId;
        
        -- 在庫マスタから当日の伝票に関連する商品のみをCP在庫マスタに挿入
        INSERT INTO CpInventoryMaster (
            ProductCode, 
            GradeCode, 
            ClassCode, 
            ShippingMarkCode, 
            ShippingMarkName,
            DataSetId,
            ProductName, 
            Unit, 
            StandardPrice, 
            ProductCategory1, 
            ProductCategory2,
            JobDate, 
            CreatedDate, 
            UpdatedDate,
            PreviousDayStock, 
            PreviousDayStockAmount, 
            PreviousDayUnitPrice,
            DailyStock, 
            DailyStockAmount, 
            DailyUnitPrice, 
            DailyFlag,
            DailySalesQuantity, 
            DailySalesAmount,
            DailySalesReturnQuantity, 
            DailySalesReturnAmount,
            DailyPurchaseQuantity, 
            DailyPurchaseAmount,
            DailyPurchaseReturnQuantity, 
            DailyPurchaseReturnAmount,
            DailyInventoryAdjustmentQuantity, 
            DailyInventoryAdjustmentAmount,
            DailyProcessingQuantity, 
            DailyProcessingAmount,
            DailyTransferQuantity, 
            DailyTransferAmount,
            DailyReceiptQuantity, 
            DailyReceiptAmount,
            DailyShipmentQuantity, 
            DailyShipmentAmount,
            DailyGrossProfit, 
            DailyWalkingAmount,
            DailyIncentiveAmount, 
            DailyDiscountAmount,
            MonthlySalesQuantity, 
            MonthlySalesAmount,
            MonthlySalesReturnQuantity, 
            MonthlySalesReturnAmount,
            MonthlyPurchaseQuantity, 
            MonthlyPurchaseAmount,
            MonthlyPurchaseReturnQuantity, 
            MonthlyPurchaseReturnAmount,
            MonthlyInventoryAdjustmentQuantity, 
            MonthlyInventoryAdjustmentAmount,
            MonthlyProcessingQuantity, 
            MonthlyProcessingAmount,
            MonthlyTransferQuantity, 
            MonthlyTransferAmount,
            MonthlyGrossProfit, 
            MonthlyWalkingAmount,
            MonthlyIncentiveAmount,
            DepartmentCode
        )
        SELECT DISTINCT
            -- キー項目（5項目）
            im.ProductCode, 
            im.GradeCode, 
            im.ClassCode, 
            im.ShippingMarkCode, 
            im.ShippingMarkName,
            -- DataSetId
            @DataSetId,
            -- 商品情報
            ISNULL(pm.ProductName, '商' + im.ProductCode),
            ISNULL(pm.Unit, 'PCS'),
            ISNULL(pm.StandardPrice, 0),
            ISNULL(pm.ProductCategory1, ''),
            ISNULL(pm.ProductCategory2, ''),
            -- 日付情報
            @JobDate,
            GETDATE(),
            GETDATE(),
            -- 前日在庫（在庫マスタの現在在庫を使用）
            ISNULL(im.CurrentStock, 0),
            ISNULL(im.CurrentStockAmount, 0),
            CASE 
                WHEN ISNULL(im.CurrentStock, 0) > 0 
                THEN ROUND(im.CurrentStockAmount / im.CurrentStock, 4)
                ELSE 0 
            END,
            -- 当日在庫（初期値として前日在庫と同じ）
            ISNULL(im.CurrentStock, 0),
            ISNULL(im.CurrentStockAmount, 0),
            CASE 
                WHEN ISNULL(im.CurrentStock, 0) > 0 
                THEN ROUND(im.CurrentStockAmount / im.CurrentStock, 4)
                ELSE 0 
            END,
            '9', -- DailyFlag
            -- 日計フィールド（すべて0で初期化）
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            -- 月計フィールド（すべて0で初期化）
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            -- 部門コード
            'DeptA'
        FROM InventoryMaster im
        LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode
        WHERE EXISTS (
            -- 当日の売上伝票に存在
            SELECT 1 FROM SalesVouchers sv
            WHERE sv.JobDate = @JobDate
                AND sv.ProductCode = im.ProductCode
                AND sv.GradeCode = im.GradeCode
                AND sv.ClassCode = im.ClassCode
                AND sv.ShippingMarkCode = im.ShippingMarkCode
                AND sv.ShippingMarkName COLLATE Japanese_CI_AS = im.ShippingMarkName COLLATE Japanese_CI_AS
        )
        OR EXISTS (
            -- 当日の仕入伝票に存在
            SELECT 1 FROM PurchaseVouchers pv
            WHERE pv.JobDate = @JobDate
                AND pv.ProductCode = im.ProductCode
                AND pv.GradeCode = im.GradeCode
                AND pv.ClassCode = im.ClassCode
                AND pv.ShippingMarkCode = im.ShippingMarkCode
                AND pv.ShippingMarkName COLLATE Japanese_CI_AS = im.ShippingMarkName COLLATE Japanese_CI_AS
        )
        OR EXISTS (
            -- 当日の在庫調整に存在
            SELECT 1 FROM InventoryAdjustments ia
            WHERE ia.JobDate = @JobDate
                AND ia.ProductCode = im.ProductCode
                AND ia.GradeCode = im.GradeCode
                AND ia.ClassCode = im.ClassCode
                AND ia.ShippingMarkCode = im.ShippingMarkCode
                AND ia.ShippingMarkName COLLATE Japanese_CI_AS = im.ShippingMarkName COLLATE Japanese_CI_AS
        );
        
        SET @CreatedCount = @@ROWCOUNT;
        
        -- 結果を返す
        SELECT @CreatedCount AS CreatedCount;
        
        COMMIT TRANSACTION;
        
        PRINT 'CP在庫マスタ作成完了: ' + CAST(@CreatedCount AS NVARCHAR(10)) + '件';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END
        
        SET @ErrorMessage = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END
GO

-- 権限設定
GRANT EXECUTE ON sp_CreateCpInventoryFromInventoryMasterCumulative TO [public];
GO

PRINT 'ストアドプロシージャ sp_CreateCpInventoryFromInventoryMasterCumulative を作成しました。';