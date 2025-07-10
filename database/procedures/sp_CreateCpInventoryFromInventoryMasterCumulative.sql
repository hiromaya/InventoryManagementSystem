-- =============================================
-- 累積管理対応版 CP在庫マスタ作成ストアドプロシージャ
-- 作成日: 2025-07-10
-- 説明: 在庫マスタからすべてのレコードをCP在庫マスタにコピーし、累積管理を実現
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
        
        -- 在庫マスタから全商品をCP在庫マスタに挿入
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
            ISNULL(pm.ProductName, N'商' + im.ProductCode),
            ISNULL(pm.Unit, N'PCS'),
            ISNULL(pm.StandardPrice, 0),
            ISNULL(pm.ProductCategory1, N''),
            ISNULL(pm.ProductCategory2, N''),
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
            N'9', -- DailyFlag
            -- 日計フィールド（すべて0で初期化）
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            -- 月計フィールド（すべて0で初期化）
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            -- 部門コード
            N'DeptA'
        FROM InventoryMaster im
        LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode;
        
        SET @CreatedCount = @@ROWCOUNT;
        
        -- 結果を返す
        SELECT @CreatedCount AS CreatedCount;
        
        COMMIT TRANSACTION;
        
        PRINT N'CP在庫マスタ作成完了: ' + CAST(@CreatedCount AS NVARCHAR(10)) + N'件';
        
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

PRINT N'ストアドプロシージャ sp_CreateCpInventoryFromInventoryMasterCumulative を作成しました。';